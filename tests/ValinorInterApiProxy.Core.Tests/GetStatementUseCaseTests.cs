using ValinorInterApiProxy.Core.Exceptions;
using ValinorInterApiProxy.Core.Models;
using ValinorInterApiProxy.Core.Ports;
using ValinorInterApiProxy.Core.UseCases;

namespace ValinorInterApiProxy.Core.Tests;

/// <summary>
/// Unit tests for <see cref="GetStatementUseCase"/>'s date-range validation (spec Edge Cases,
/// FR-007). Each case asserts the use case never touches Inter (<see cref="IInterAccessTokenProvider"/>
/// / <see cref="IInterStatementClient"/> mocked) once validation fails.
/// </summary>
public sealed class GetStatementUseCaseTests
{
    private const int MaxPeriodDays = 90;

    [Fact]
    public async Task ExecuteAsync_EndDateBeforeStartDate_ThrowsValidationErrorWithoutCallingInter()
    {
        var tokenProvider = new RecordingAccessTokenProvider();
        var statementClient = new RecordingStatementClient();
        var useCase = new GetStatementUseCase(tokenProvider, statementClient, MaxPeriodDays);
        var query = new StatementQuery(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 5));

        await Assert.ThrowsAsync<StatementValidationException>(() => useCase.ExecuteAsync(query));

        Assert.False(tokenProvider.WasCalled);
        Assert.False(statementClient.WasCalled);
    }

    [Fact]
    public async Task ExecuteAsync_EndDateInFuture_ThrowsValidationErrorWithoutCallingInter()
    {
        var tokenProvider = new RecordingAccessTokenProvider();
        var statementClient = new RecordingStatementClient();
        var useCase = new GetStatementUseCase(tokenProvider, statementClient, MaxPeriodDays);
        var futureEndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var query = new StatementQuery(futureEndDate.AddDays(-5), futureEndDate);

        await Assert.ThrowsAsync<StatementValidationException>(() => useCase.ExecuteAsync(query));

        Assert.False(tokenProvider.WasCalled);
        Assert.False(statementClient.WasCalled);
    }

    [Fact]
    public async Task ExecuteAsync_RangeExceedsMaximumPeriod_ThrowsValidationErrorWithoutCallingInter()
    {
        var tokenProvider = new RecordingAccessTokenProvider();
        var statementClient = new RecordingStatementClient();
        var useCase = new GetStatementUseCase(tokenProvider, statementClient, MaxPeriodDays);
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-(MaxPeriodDays + 10));
        var query = new StatementQuery(startDate, endDate);

        await Assert.ThrowsAsync<StatementValidationException>(() => useCase.ExecuteAsync(query));

        Assert.False(tokenProvider.WasCalled);
        Assert.False(statementClient.WasCalled);
    }

    private sealed class RecordingAccessTokenProvider : IInterAccessTokenProvider
    {
        public bool WasCalled { get; private set; }

        public Task<InterAccessToken> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            throw new InvalidOperationException("Should not be called for an invalid StatementQuery.");
        }
    }

    private sealed class RecordingStatementClient : IInterStatementClient
    {
        public bool WasCalled { get; private set; }

        public Task<BankStatement> GetStatementAsync(
            StatementQuery query,
            InterAccessToken accessToken,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            throw new InvalidOperationException("Should not be called for an invalid StatementQuery.");
        }
    }
}
