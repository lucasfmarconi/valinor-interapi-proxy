using ValinorInterApiProxy.Core.Models;
using ValinorInterApiProxy.Core.Ports;

namespace ValinorInterApiProxy.Core.UseCases;

/// <summary>
/// Orchestrates Banco Inter access token issuance (spec FR-004): delegates directly to
/// <see cref="IInterTokenClient"/>, with no caching — unlike <see cref="GetStatementUseCase"/>,
/// Token issuance always requests a fresh Banco Inter token per spec Assumptions.
/// </summary>
public sealed class IssueTokenUseCase(IInterTokenClient tokenClient)
{
    public Task<InterAccessToken> ExecuteAsync(CancellationToken cancellationToken = default) =>
        tokenClient.IssueTokenAsync(cancellationToken);
}
