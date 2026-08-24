using ValinorInterApiProxy.Core.Models;

namespace ValinorInterApiProxy.Core.Ports;

/// <summary>
/// Retrieves a Banco Inter bank statement over MTLS using an already-issued access token.
/// Implemented by Infrastructure; Core and Api depend only on this interface (constitution
/// Principle I).
/// </summary>
public interface IInterStatementClient
{
    Task<BankStatement> GetStatementAsync(
        StatementQuery query,
        InterAccessToken accessToken,
        CancellationToken cancellationToken = default);
}
