using ValinorInterApiProxy.Core.Models;

namespace ValinorInterApiProxy.Core.Ports;

/// <summary>
/// Supplies a valid, internally-cached Banco Inter access token for the Extrato capability,
/// obtaining and refreshing it via <see cref="IInterTokenClient"/> as needed (spec FR-005). Not
/// consumer-facing.
/// </summary>
public interface IInterAccessTokenProvider
{
    Task<InterAccessToken> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
