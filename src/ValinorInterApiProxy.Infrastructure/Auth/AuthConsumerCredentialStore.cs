using Microsoft.Extensions.Options;
using ValinorInterApiProxy.Core.Models;
using ValinorInterApiProxy.Core.Ports;

namespace ValinorInterApiProxy.Infrastructure.Auth;

/// <summary>
/// Looks up registered <see cref="AuthConsumer"/>s from the <c>Jwt:Consumers</c> configuration
/// (constitution Principle II). A simple in-memory, config-backed store — adequate for this
/// proxy's local/dev-grade consumer registry; not a general-purpose secrets manager.
/// </summary>
public sealed class AuthConsumerCredentialStore(IOptions<AuthConsumersOptions> options) : IClientCredentialStore
{
    public AuthConsumer? FindByClientId(string clientId)
    {
        var entry = options.Value.Consumers
            .FirstOrDefault(c => string.Equals(c.ClientId, clientId, StringComparison.Ordinal));

        return entry is null ? null : new AuthConsumer(entry.ClientId, entry.ClientSecret, entry.AllowedScopes);
    }
}