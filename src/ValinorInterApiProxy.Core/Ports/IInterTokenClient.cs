using ValinorInterApiProxy.Core.Models;

namespace ValinorInterApiProxy.Core.Ports;

/// <summary>
/// Issues Banco Inter OAuth access tokens over MTLS. Implemented by Infrastructure; Core and Api
/// depend only on this interface (constitution Principle I).
/// </summary>
public interface IInterTokenClient
{
    Task<InterAccessToken> IssueTokenAsync(CancellationToken cancellationToken = default);
}
