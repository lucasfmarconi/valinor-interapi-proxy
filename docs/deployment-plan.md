# Plan: Docker support + GitHub build workflow

Status: **not started**. Rev 4, 2026-10-06. Handoff for implementation in Claude Code.

## Scope

**In scope (this pass)**

1. Docker support in the repo: `Dockerfile`, `.dockerignore`, a compose file, `.env.example`.
2. GitHub Actions on **GitHub-hosted runners only**: test on pull requests; on push to `main`,
   test, build the image and push it to GHCR.

**Out of scope (deferred)**

- Any deploy automation on the Hetzner server. No self-hosted runner, Jenkins, Ansible or
  timers for now. Deploying is a manual `docker compose pull && up -d` on the server.
  See "Deferred: deploy automation" at the end for the options already discussed.

## Decisions

| Topic | Decision |
|---|---|
| Image registry | GHCR: `ghcr.io/lucasfmarconi/valinor-interapi-proxy` |
| Runners | GitHub-hosted only (the repo is public, so nothing runs on the server) |
| Image platform | `linux/amd64` (x86) only |
| Tags | `<commit sha>` and `latest` on every push to `main` |
| Exposure | Internal only: compose binds to `127.0.0.1` and a named Docker network. No reverse proxy, no TLS |
| Health check | No `/healthz`. Smoke test is an empty `POST /auth/token`, passing on any status below 500 |
| Secrets | `.env` and the `.pfx` are supplied at run time; never in the image, the repo, or GitHub |

## Current repo state (checked 2026-10-05)

- .NET 10, solution `ValinorInterApiProxy.slnx`, entry project `src/ValinorInterApiProxy.Api`.
- `Dockerfile` exists at the repo root but is **empty and untracked**. No `.dockerignore`,
  no compose file, no `.github/` directory.
- `src/ValinorInterApiProxy.Api/Inter_API.pfx` and `secrets.sh` sit inside the project folder.
  Both are gitignored and were never committed. They must also be kept out of the Docker
  build context.
- `Inter:BaseUrl` is set **only** in `appsettings.Development.json` (as `BaseURl`), and
  `InterOptions.BaseUrl` is `required`. Production must supply `Inter__BaseUrl`.
- `Program.cs` calls `app.UseHttpsRedirection()`; the container serves plain HTTP on 8080.
- `Inter:CertificatePath` is loaded with `X509CertificateLoader.LoadPkcs12FromFile`
  (`Infrastructure/InterApi/InterHttpClient.cs`), so a mounted file path works as-is.
- Tests need no credentials (Banco Inter is stubbed with WireMock), so they run in CI as-is.

## Phase 1: Docker support

- [ ] **`.dockerignore`** at the repo root:
  ```
  **/bin
  **/obj
  **/*.pfx
  **/secrets.sh
  **/.env
  .git
  .github
  .idea
  .claude
  .specify
  specs
  tests
  docs
  ```
- [ ] **`Dockerfile`** (replace the empty file), multi-stage:
  ```dockerfile
  FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
  WORKDIR /src
  COPY src/ValinorInterApiProxy.Core/ValinorInterApiProxy.Core.csproj src/ValinorInterApiProxy.Core/
  COPY src/ValinorInterApiProxy.Infrastructure/ValinorInterApiProxy.Infrastructure.csproj src/ValinorInterApiProxy.Infrastructure/
  COPY src/ValinorInterApiProxy.Api/ValinorInterApiProxy.Api.csproj src/ValinorInterApiProxy.Api/
  RUN dotnet restore src/ValinorInterApiProxy.Api/ValinorInterApiProxy.Api.csproj
  COPY src/ src/
  RUN dotnet publish src/ValinorInterApiProxy.Api/ValinorInterApiProxy.Api.csproj \
      -c Release --no-restore -o /app

  FROM mcr.microsoft.com/dotnet/aspnet:10.0
  WORKDIR /app
  COPY --from=build /app .
  USER $APP_UID
  EXPOSE 8080
  ENTRYPOINT ["dotnet", "ValinorInterApiProxy.Api.dll"]
  ```
  Tests are not run in the image build; the workflow's `test` job covers them.
  The Dockerfile is platform-neutral: CI builds it for x86, while a local build on the
  Apple Silicon Mac produces a native arm64 image, which is fine for local testing.
- [ ] **`docker-compose.yml`** at the repo root (used locally and, later, on the server):
  ```yaml
  services:
    api:
      image: ghcr.io/lucasfmarconi/valinor-interapi-proxy:${IMAGE_TAG:-latest}
      build: .
      restart: unless-stopped
      env_file: .env
      environment:
        ASPNETCORE_ENVIRONMENT: Production
        Inter__CertificatePath: /run/secrets/inter.pfx
      volumes:
        - ${INTER_PFX_PATH:-./secrets/Inter_API.pfx}:/run/secrets/inter.pfx:ro
      ports:
        - "127.0.0.1:${API_PORT:-8080}:8080"   # host processes only; never 0.0.0.0
      networks:
        interproxy:
          aliases:
            - inter-proxy

  networks:
    interproxy:
      name: interproxy
  ```
  `docker compose up -d --build` builds locally; `docker compose pull && docker compose up -d`
  uses the published image. Other compose stacks reach the service at `http://inter-proxy:8080`
  by declaring the `interproxy` network as `external: true`.
- [ ] **`.env.example`** at the repo root (names only, no secret values), mapping `:` to `__`:
  ```
  API_PORT=8080
  INTER_PFX_PATH=./secrets/Inter_API.pfx
  Inter__BaseUrl=https://cdpj.partners.bancointer.com.br
  Inter__ClientId=
  Inter__ClientSecret=
  Inter__CertificatePassword=
  Inter__Scope=extrato.read
  Inter__ContaCorrente=
  Jwt__SigningKey=
  Jwt__Issuer=valinor-interapi-proxy
  Jwt__Audience=valinor-interapi-proxy-consumers
  Jwt__Consumers__0__ClientId=
  Jwt__Consumers__0__ClientSecret=
  Jwt__Consumers__0__AllowedScopes__0=token-issue
  Jwt__Consumers__0__AllowedScopes__1=extrato-read
  ```
  Wrap values containing `$` in single quotes so Compose does not interpolate them.
- [ ] **`.gitignore`**: confirm `.env` and `secrets/` are ignored and `.env.example` is not.
- [ ] **HTTPS redirection over plain HTTP**: with no HTTPS port configured,
  `UseHttpsRedirection` should only log a warning and not redirect. Verify that
  `POST http://localhost:8080/auth/token` is answered directly (not 307). If it redirects,
  make the call conditional (e.g. Development only) and cover it with a test.
- [ ] **Certificate readability**: the container runs as the non-root `APP_UID` (1654). The
  mounted `.pfx` must be readable by that uid (on Linux: `chown 1654` + `chmod 400`).

### Phase 1 verification (local, on the Mac)

- [ ] `docker build -t interproxy:local .` succeeds.
- [ ] No secrets in the image:
  `docker run --rm --entrypoint sh interproxy:local -c "find / -name '*.pfx' -o -name 'secrets.sh' 2>/dev/null"`
  prints nothing.
- [ ] `cp .env.example .env`, fill real values, `docker compose up -d --build`; the container
  stays up and `docker compose logs api` shows no options-validation failure
  (`ValidateOnStart` fails fast on missing config).
- [ ] `curl -s -o /dev/null -w '%{http_code}\n' -X POST http://127.0.0.1:8080/auth/token`
  returns a 4xx (not 5xx, not 307).
- [ ] A real `/auth/token` then `/extrato` call works, proving the mounted certificate is used.

## Phase 2: GitHub workflows

No repository secrets are needed: pushing to GHCR uses the built-in `GITHUB_TOKEN`.

- [ ] **`.github/workflows/ci.yml`** (pull requests: test and prove the image builds, no push):
  ```yaml
  name: ci

  on:
    pull_request:

  permissions:
    contents: read

  jobs:
    test:
      runs-on: ubuntu-latest
      steps:
        - uses: actions/checkout@v4
        - uses: actions/setup-dotnet@v4
          with:
            dotnet-version: 10.0.x
        - run: dotnet test

    docker-build:
      runs-on: ubuntu-latest
      steps:
        - uses: actions/checkout@v4
        - uses: docker/setup-buildx-action@v3
        - uses: docker/build-push-action@v6
          with:
            context: .
            push: false
  ```
- [ ] **`.github/workflows/publish.yml`** (push to `main`: test, then build and push):
  ```yaml
  name: publish

  on:
    push:
      branches: [main]
    workflow_dispatch:

  concurrency:
    group: publish-${{ github.ref }}
    cancel-in-progress: false

  permissions:
    contents: read

  env:
    IMAGE: ghcr.io/lucasfmarconi/valinor-interapi-proxy

  jobs:
    test:
      runs-on: ubuntu-latest
      steps:
        - uses: actions/checkout@v4
        - uses: actions/setup-dotnet@v4
          with:
            dotnet-version: 10.0.x
        - run: dotnet test

    build-push:
      needs: test
      runs-on: ubuntu-latest
      permissions:
        contents: read
        packages: write
      steps:
        - uses: actions/checkout@v4
        - uses: docker/setup-buildx-action@v3
        - uses: docker/login-action@v3
          with:
            registry: ghcr.io
            username: ${{ github.actor }}
            password: ${{ secrets.GITHUB_TOKEN }}
        - uses: docker/build-push-action@v6
          with:
            context: .
            platforms: linux/amd64
            push: true
            tags: |
              ${{ env.IMAGE }}:${{ github.sha }}
              ${{ env.IMAGE }}:latest
            cache-from: type=gha
            cache-to: type=gha,mode=max
  ```
  Things to verify while implementing:
  - Action major versions are current; consider pinning actions to commit SHAs.
  - Whether the tests need Docker or anything else not on `ubuntu-latest` (they should not).
  - Server architecture is confirmed x86 (`uname -m` prints `x86_64`, checked 2026-10-06),
    so the `linux/amd64` image matches.

### Phase 2 verification

- [ ] Open a PR: `ci` runs both jobs green and pushes nothing.
- [ ] Merge to `main`: `publish` goes green and the package appears under the repo's
  **Packages** with both `<sha>` and `latest` tags, for `linux/amd64`.
- [ ] **Check the package visibility** in its settings. The image contains no secrets, so
  public is acceptable for a public repo; set it to private if you prefer (the server then
  needs a token with `read:packages` to pull).
- [ ] `docker pull --platform linux/amd64 ghcr.io/lucasfmarconi/valinor-interapi-proxy:latest`
  works from the Mac (it runs there under emulation; use a local build for day-to-day testing).

## Phase 3: Docs

- [ ] Add a short "Running with Docker" section to `README.md`: build, `.env`, certificate
  mount, compose commands, and where published images live.

## Manual deploy on the server (until automation is decided)

Not part of the implementation; recorded so the image can be used right away.

```bash
# one time
sudo mkdir -p /opt/valinor-interapi-proxy/secrets && cd /opt/valinor-interapi-proxy
# copy docker-compose.yml, a filled-in .env, and secrets/Inter_API.pfx here
sudo chown 1654:1654 secrets/Inter_API.pfx && sudo chmod 400 secrets/Inter_API.pfx
chmod 600 .env
# only if the GHCR package is private:
#   docker login ghcr.io -u lucasfmarconi   (token with read:packages)

# every deploy (set IMAGE_TAG=<sha> to pin or roll back)
docker compose pull && docker compose up -d
curl -s -o /dev/null -w '%{http_code}\n' -X POST http://127.0.0.1:8080/auth/token
```

On the server, remove or ignore the `build: .` line; there is no source there.

## Security checklist

- [ ] Image contains no secrets (Phase 1 check); `.dockerignore` excludes `.pfx`, `secrets.sh`, `.env`.
- [ ] `.env` and `secrets/` are gitignored; `.env.example` has no real values.
- [ ] Workflows run only on GitHub-hosted runners; `packages: write` is granted only to the
  `build-push` job, which never runs for pull requests.
- [ ] Compose binds to `127.0.0.1`, never `0.0.0.0` (Docker-published ports bypass `ufw`).
- [ ] Traffic between consumers and the proxy is plain HTTP; acceptable only because it never
  leaves the machine.

## Deferred: deploy automation

Options discussed, to revisit later. The constraint is that the repo is **public**, and a
self-hosted runner on a public repo lets a fork's pull request target the server.

| Option | Notes |
|---|---|
| Self-hosted GitHub runner on the server | Only sensible if the repo becomes private (or with strict fork-approval settings). Gives push-triggered deploys and an approval gate via a `production` environment. |
| Ansible from the Mac | Agentless, repo can stay public, running the playbook is the manual approval. Can also provision the server. |
| Pull-based script / systemd timer | Server runs `docker compose pull && up -d` itself. Smallest footprint; no approval gate if on a timer. |
| Jenkins on the server | Works with a public repo (build `main` only) but is a heavy service to patch and protect on a machine holding bank credentials. |
