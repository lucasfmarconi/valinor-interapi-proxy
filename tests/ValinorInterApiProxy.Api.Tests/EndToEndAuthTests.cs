using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ValinorInterApiProxy.Api.Contracts;
using ValinorInterApiProxy.Api.Tests.Infrastructure;
using ValinorInterApiProxy.Core.Models;

namespace ValinorInterApiProxy.Api.Tests;

/// <summary>
/// End-to-end proof that a JWT minted by <c>POST /auth/token</c> is accepted by the real JWT
/// bearer pipeline (constitution Principle II) — not the <see cref="TestAuthHandler"/> fake used
/// elsewhere. This is the test that would catch a regression like <c>MapInboundClaims</c> being
/// left at its default (which silently breaks the "sub" claim the rest of the app relies on).
/// </summary>
public sealed class EndToEndAuthTests : IClassFixture<RealAuthWebApplicationFactory>
{
    private readonly RealAuthWebApplicationFactory _factory;

    public EndToEndAuthTests(RealAuthWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.TokenClient.TokenToReturn = new InterAccessToken(
            "fake-access-token", "Bearer", DateTimeOffset.UtcNow.AddHours(1), "extrato.read");
    }

    [Fact]
    public async Task IssuedToken_AuthorizesBothTokenAndExtratoEndpoints()
    {
        var client = _factory.CreateClient();

        var authResponse = await client.PostAsync("/auth/token", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("client_id", "test-consumer"),
            new KeyValuePair<string, string>("client_secret", "test-secret"),
            new KeyValuePair<string, string>("scope", "token-issue extrato-read"),
        ]));
        Assert.Equal(HttpStatusCode.OK, authResponse.StatusCode);
        var issued = await authResponse.Content.ReadFromJsonAsync<AuthTokenResponse>();
        Assert.NotNull(issued);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", issued!.AccessToken);

        var tokenResponse = await client.PostAsync("/token", new StringContent(string.Empty));
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);

        var extratoResponse = await client.GetAsync("/extrato?startDate=2026-07-01&endDate=2026-07-31");
        Assert.Equal(HttpStatusCode.OK, extratoResponse.StatusCode);
    }

    [Fact]
    public async Task IssuedTokenWithOnlyExtratoScope_IsRejectedForTokenEndpoint()
    {
        var client = _factory.CreateClient();

        var authResponse = await client.PostAsync("/auth/token", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("client_id", "test-consumer"),
            new KeyValuePair<string, string>("client_secret", "test-secret"),
            new KeyValuePair<string, string>("scope", "extrato-read"),
        ]));
        var issued = await authResponse.Content.ReadFromJsonAsync<AuthTokenResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", issued!.AccessToken);

        var tokenResponse = await client.PostAsync("/token", new StringContent(string.Empty));
        Assert.Equal(HttpStatusCode.Forbidden, tokenResponse.StatusCode);
    }
}