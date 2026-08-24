using System.Net;
using ValinorInterApiProxy.Api.Tests.Infrastructure;

namespace ValinorInterApiProxy.Api.Tests;

/// <summary>
/// Authorization-rejection tests for <c>POST /token</c> (spec Acceptance Scenario US2.2,
/// SC-002): a missing JWT or a JWT lacking the <c>token-issue</c> scope must be denied.
/// </summary>
public sealed class TokenAuthorizationTests : IClassFixture<ProxyWebApplicationFactory>
{
    private readonly ProxyWebApplicationFactory _factory;

    public TokenAuthorizationTests(ProxyWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PostToken_WithoutJwt_ReturnsUnauthorizedAndNeverCallsInter()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/token", new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(_factory.TokenClient.WasCalled);
    }

    [Fact]
    public async Task PostToken_WithJwtMissingTokenIssueScope_ReturnsForbiddenAndNeverCallsInter()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, "consumer-app");
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "extrato-read");

        var response = await client.PostAsync("/token", new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(_factory.TokenClient.WasCalled);
    }
}
