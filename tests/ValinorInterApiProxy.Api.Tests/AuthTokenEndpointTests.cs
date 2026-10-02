using System.Net;
using System.Net.Http.Json;
using ValinorInterApiProxy.Api.Contracts;
using ValinorInterApiProxy.Api.Tests.Infrastructure;

namespace ValinorInterApiProxy.Api.Tests;

/// <summary>
/// Tests for <c>POST /auth/token</c> (constitution Principle II) against the consumer registered
/// in <see cref="ProxyWebApplicationFactory"/>'s <c>Jwt:Consumers</c> test configuration
/// (<c>test-consumer</c> / <c>test-secret</c>, allowed scopes <c>token-issue</c> and
/// <c>extrato-read</c>).
/// </summary>
public sealed class AuthTokenEndpointTests : IClassFixture<ProxyWebApplicationFactory>
{
    private readonly ProxyWebApplicationFactory _factory;

    public AuthTokenEndpointTests(ProxyWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PostAuthToken_WithValidCredentials_ReturnsAccessToken()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/auth/token", Form(
            ("client_id", "test-consumer"), ("client_secret", "test-secret"), ("scope", "extrato-read")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthTokenResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrEmpty(body!.AccessToken));
        Assert.Equal("Bearer", body.TokenType);
        Assert.True(body.ExpiresIn > 0);
    }

    [Fact]
    public async Task PostAuthToken_WithWrongSecret_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/auth/token", Form(
            ("client_id", "test-consumer"), ("client_secret", "wrong-secret")));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostAuthToken_WithScopeOutsideAllowlist_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/auth/token", Form(
            ("client_id", "test-consumer"), ("client_secret", "test-secret"), ("scope", "not-allowed")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAuthToken_WithMissingClientId_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/auth/token", Form(("client_secret", "test-secret")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static FormUrlEncodedContent Form(params (string Key, string Value)[] fields) =>
        new(fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)));
}