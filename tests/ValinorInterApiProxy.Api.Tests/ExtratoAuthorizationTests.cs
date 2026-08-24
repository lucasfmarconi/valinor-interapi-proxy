using System.Net;
using ValinorInterApiProxy.Api.Tests.Infrastructure;

namespace ValinorInterApiProxy.Api.Tests;

/// <summary>
/// Authorization-rejection tests for <c>GET /extrato</c> (spec Acceptance Scenario US1.2,
/// SC-002): a missing JWT or a JWT lacking the <c>extrato-read</c> scope must be denied before
/// any Inter call is made.
/// </summary>
public sealed class ExtratoAuthorizationTests : IClassFixture<ProxyWebApplicationFactory>
{
    private readonly ProxyWebApplicationFactory _factory;

    public ExtratoAuthorizationTests(ProxyWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetExtrato_WithoutJwt_ReturnsUnauthorizedAndNeverCallsInter()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/extrato?startDate=2024-04-01&endDate=2024-04-10");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(_factory.AccessTokenProvider.WasCalled);
        Assert.False(_factory.StatementClient.WasCalled);
    }

    [Fact]
    public async Task GetExtrato_WithJwtMissingExtratoReadScope_ReturnsForbiddenAndNeverCallsInter()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, "consumer-app");
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "token-issue");

        var response = await client.GetAsync("/extrato?startDate=2024-04-01&endDate=2024-04-10");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(_factory.AccessTokenProvider.WasCalled);
        Assert.False(_factory.StatementClient.WasCalled);
    }
}
