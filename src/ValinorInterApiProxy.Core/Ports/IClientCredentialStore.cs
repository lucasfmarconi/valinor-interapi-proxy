using ValinorInterApiProxy.Core.Models;

namespace ValinorInterApiProxy.Core.Ports;

/// <summary>
/// Looks up registered <see cref="AuthConsumer"/>s by client id. Implemented by Infrastructure;
/// Core and Api depend only on this interface (constitution Principle I).
/// </summary>
public interface IClientCredentialStore
{
    AuthConsumer? FindByClientId(string clientId);
}