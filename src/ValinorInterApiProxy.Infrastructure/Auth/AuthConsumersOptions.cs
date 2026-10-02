namespace ValinorInterApiProxy.Infrastructure.Auth;

/// <summary>
/// Registered <c>/auth/token</c> consumers, bound from the same <c>Jwt</c> configuration section
/// as <see cref="JwtIssuerOptions"/> (sub-key <c>Jwt:Consumers</c>).
/// </summary>
public sealed class AuthConsumersOptions
{
    public IReadOnlyList<AuthConsumerEntry> Consumers { get; set; } = [];
}

public sealed class AuthConsumerEntry
{
    public required string ClientId { get; set; }

    public required string ClientSecret { get; set; }

    public required string[] AllowedScopes { get; set; }
}