using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ValinorInterApiProxy.Core.Ports;

namespace ValinorInterApiProxy.Api.Tests.Infrastructure;

/// <summary>
/// A <see cref="WebApplicationFactory{TEntryPoint}"/> for <c>Program</c> that keeps the real
/// self-issued JWT bearer pipeline (unlike <see cref="ProxyWebApplicationFactory"/>, which swaps
/// it for <see cref="TestAuthHandler"/>), only replacing the Infrastructure-layer Inter ports.
/// Used to prove a token minted by <c>POST /auth/token</c> is genuinely accepted by the real
/// <c>JwtBearerHandler</c>/<c>ScopeAuthorizationHandler</c>, not just structurally well-formed.
/// </summary>
public sealed class RealAuthWebApplicationFactory : WebApplicationFactory<Program>
{
    public FakeInterAccessTokenProvider AccessTokenProvider { get; } = new();

    public FakeInterStatementClient StatementClient { get; } = new();

    public FakeInterTokenClient TokenClient { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = Convert.ToBase64String("test-signing-key-at-least-32-bytes-long"u8.ToArray()),
            ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience",
            ["Jwt:Consumers:0:ClientId"] = "test-consumer",
            ["Jwt:Consumers:0:ClientSecret"] = "test-secret",
            ["Jwt:Consumers:0:AllowedScopes:0"] = "token-issue",
            ["Jwt:Consumers:0:AllowedScopes:1"] = "extrato-read",
        }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IInterAccessTokenProvider>();
            services.AddSingleton<IInterAccessTokenProvider>(AccessTokenProvider);

            services.RemoveAll<IInterStatementClient>();
            services.AddSingleton<IInterStatementClient>(StatementClient);

            services.RemoveAll<IInterTokenClient>();
            services.AddSingleton<IInterTokenClient>(TokenClient);
        });
    }
}