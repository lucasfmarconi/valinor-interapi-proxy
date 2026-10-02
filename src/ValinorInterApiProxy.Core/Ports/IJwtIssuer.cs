using ValinorInterApiProxy.Core.Models;

namespace ValinorInterApiProxy.Core.Ports;

/// <summary>
/// Signs JWTs for self-issuance to registered consumers (constitution Principle II). Implemented
/// by Infrastructure; Core and Api depend only on this interface.
/// </summary>
public interface IJwtIssuer
{
    IssuedAuthToken Issue(string subject, IReadOnlyCollection<string> scopes);
}