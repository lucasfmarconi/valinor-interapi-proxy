namespace ValinorInterApiProxy.Infrastructure.Auth;

/// <summary>
/// Settings for the proxy's own self-issued JWTs (constitution Principle II), bound via the
/// Options pattern from secret configuration (.NET User Secrets in development; environment/Key
/// Vault in production). No literal secret values may appear in source (constitution Principle
/// IV).
/// </summary>
public sealed class JwtIssuerOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Base64-encoded symmetric signing key, must decode to at least 32 bytes (256 bits).</summary>
    public required string SigningKey { get; set; }

    public required string Issuer { get; set; }

    public required string Audience { get; set; }

    public int AccessTokenLifetimeMinutes { get; set; } = 60;
}