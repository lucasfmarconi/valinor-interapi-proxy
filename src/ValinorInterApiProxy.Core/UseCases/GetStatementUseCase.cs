using ValinorInterApiProxy.Core.Exceptions;
using ValinorInterApiProxy.Core.Models;
using ValinorInterApiProxy.Core.Ports;

namespace ValinorInterApiProxy.Core.UseCases;

/// <summary>
/// Orchestrates statement retrieval: validates the requested period, obtains a cached Inter
/// access token via <see cref="IInterAccessTokenProvider"/>, and delegates to
/// <see cref="IInterStatementClient"/> (spec FR-006/FR-007). <paramref name="maxPeriodDays"/> is
/// sourced from configuration rather than hardcoded, per data-model.md.
/// </summary>
public sealed class GetStatementUseCase(
    IInterAccessTokenProvider accessTokenProvider,
    IInterStatementClient statementClient,
    int maxPeriodDays)
{
    public async Task<BankStatement> ExecuteAsync(
        StatementQuery query,
        CancellationToken cancellationToken = default)
    {
        Validate(query);

        var accessToken = await accessTokenProvider.GetAccessTokenAsync(cancellationToken);
        return await statementClient.GetStatementAsync(query, accessToken, cancellationToken);
    }

    private void Validate(StatementQuery query)
    {
        if (query.EndDate < query.StartDate)
        {
            throw new StatementValidationException("endDate must not be before startDate.");
        }

        if (query.EndDate > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new StatementValidationException("endDate must not be in the future.");
        }

        if (query.EndDate.DayNumber - query.StartDate.DayNumber > maxPeriodDays)
        {
            throw new StatementValidationException(
                $"The requested period exceeds the maximum of {maxPeriodDays} days.");
        }
    }
}
