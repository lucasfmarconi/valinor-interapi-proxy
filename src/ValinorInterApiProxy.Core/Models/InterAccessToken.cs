namespace ValinorInterApiProxy.Core.Models;

/// <summary>
/// A Banco Inter OAuth access token. Never logged in the clear (constitution Principle IV/V).
/// </summary>
public sealed record InterAccessToken(string Value, string TokenType, DateTimeOffset ExpiresAt, string Scope)
{
    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
}
