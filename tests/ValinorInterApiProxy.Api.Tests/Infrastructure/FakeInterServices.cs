using ValinorInterApiProxy.Core.Models;
using ValinorInterApiProxy.Core.Ports;

namespace ValinorInterApiProxy.Api.Tests.Infrastructure;

/// <summary>
/// Hand-rolled fake standing in for the Infrastructure-layer Inter access token provider in
/// Api-level tests, so those tests never touch MTLS or a real Banco Inter call (plan.md Testing
/// strategy: "Inter ports mocked").
/// </summary>
public sealed class FakeInterAccessTokenProvider : IInterAccessTokenProvider
{
    public bool WasCalled { get; private set; }

    public InterAccessToken TokenToReturn { get; set; } =
        new("fake-access-token", "Bearer", DateTimeOffset.UtcNow.AddMinutes(5), "extrato.read");

    public Task<InterAccessToken> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        WasCalled = true;
        return Task.FromResult(TokenToReturn);
    }
}

/// <summary>
/// Hand-rolled fake standing in for the Infrastructure-layer Inter statement client in
/// Api-level tests (see <see cref="FakeInterAccessTokenProvider"/>).
/// </summary>
public sealed class FakeInterStatementClient : IInterStatementClient
{
    public bool WasCalled { get; private set; }

    public BankStatement StatementToReturn { get; set; } =
        new(new DateOnly(2024, 4, 1), new DateOnly(2024, 4, 10), []);

    public Task<BankStatement> GetStatementAsync(
        StatementQuery query,
        InterAccessToken accessToken,
        CancellationToken cancellationToken = default)
    {
        WasCalled = true;
        return Task.FromResult(StatementToReturn);
    }
}
