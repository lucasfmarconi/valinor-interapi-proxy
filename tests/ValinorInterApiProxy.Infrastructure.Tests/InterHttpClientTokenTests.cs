using Microsoft.Extensions.Options;
using ValinorInterApiProxy.Core.Exceptions;
using ValinorInterApiProxy.Infrastructure.InterApi;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace ValinorInterApiProxy.Infrastructure.Tests;

/// <summary>
/// Integration tests for <see cref="InterHttpClient.IssueTokenAsync"/> against a
/// <see cref="WireMockServer"/> standing in for Banco Inter's <c>POST /oauth/v2/token</c> (no
/// live MTLS dependency; per plan.md's Testing strategy).
/// </summary>
public sealed class InterHttpClientTokenTests : IDisposable
{
    private const string ClientId = "test-client-id";
    private const string ClientSecret = "test-client-secret";
    private const string Scope = "extrato.read";

    private readonly WireMockServer _server = WireMockServer.Start();

    [Fact]
    public async Task IssueTokenAsync_Success_MapsResponseToInterAccessToken()
    {
        _server
            .Given(Request.Create()
                .WithPath("/oauth/v2/token")
                .WithBody(body => body is not null
                    && body.Contains($"client_id={ClientId}", StringComparison.Ordinal)
                    && body.Contains($"client_secret={ClientSecret}", StringComparison.Ordinal)
                    && body.Contains($"scope={Scope}", StringComparison.Ordinal)
                    && body.Contains("grant_type=client_credentials", StringComparison.Ordinal))
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                    {
                      "access_token": "fake-access-token",
                      "token_type": "Bearer",
                      "expires_in": 3600,
                      "scope": "extrato.read"
                    }
                    """));

        var client = CreateClient();
        var before = DateTimeOffset.UtcNow;

        var token = await client.IssueTokenAsync();

        Assert.Equal("fake-access-token", token.Value);
        Assert.Equal("Bearer", token.TokenType);
        Assert.Equal("extrato.read", token.Scope);
        Assert.InRange(token.ExpiresAt, before.AddSeconds(3600 - 5), before.AddSeconds(3600 + 5));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(503)]
    public async Task IssueTokenAsync_UpstreamErrorStatus_ThrowsInterApiExceptionWithStatus(int statusCode)
    {
        _server
            .Given(Request.Create().WithPath("/oauth/v2/token").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(statusCode));

        var client = CreateClient();

        var exception = await Assert.ThrowsAsync<InterApiException>(() => client.IssueTokenAsync());

        Assert.Equal(statusCode, exception.UpstreamStatusCode);
    }

    private InterHttpClient CreateClient()
    {
        var httpClient = new HttpClient { BaseAddress = new Uri(_server.Urls[0]) };
        var options = Options.Create(new InterOptions
        {
            ClientId = ClientId,
            ClientSecret = ClientSecret,
            CertificatePath = "unused.pfx",
            CertificatePassword = "unused",
            Scope = Scope,
            ContaCorrente = "1234-5",
            BaseUrl = _server.Urls[0],
        });

        return new InterHttpClient(httpClient, options);
    }

    public void Dispose() => _server.Dispose();
}
