using System.Net;
using System.Net.Http.Json;
using ValinorInterApiProxy.Api.Tests.Infrastructure;
using ValinorInterApiProxy.Core.Models;

namespace ValinorInterApiProxy.Api.Tests;

/// <summary>
/// Contract test for <c>POST /token</c>'s success response shape, matching
/// <c>contracts/token.openapi.yaml</c>'s <c>TokenResponse</c> schema.
/// </summary>
public sealed class TokenEndpointTests : IClassFixture<ProxyWebApplicationFactory>
{
    private readonly ProxyWebApplicationFactory _factory;

    public TokenEndpointTests(ProxyWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.TokenClient.TokenToReturn = new InterAccessToken(
            "fake-access-token",
            "Bearer",
            DateTimeOffset.UtcNow.AddHours(1),
            "extrato.read");
    }

    [Fact]
    public async Task PostToken_WithValidScope_ReturnsTokenMatchingContract()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, "consumer-app");
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "token-issue");

        var response = await client.PostAsync("/token", new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TokenResponseDto>();
        Assert.NotNull(body);
        Assert.Equal(_factory.TokenClient.TokenToReturn.Value, body!.AccessToken);
        Assert.Equal(_factory.TokenClient.TokenToReturn.TokenType, body.TokenType);
        Assert.Equal(_factory.TokenClient.TokenToReturn.ExpiresAt, body.ExpiresAt);
    }

    private sealed record TokenResponseDto(string AccessToken, string TokenType, DateTimeOffset ExpiresAt);
}
