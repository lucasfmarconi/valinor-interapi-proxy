namespace ValinorInterApiProxy.Core.Models;

/// <summary>
/// A JWT self-issued by this proxy to a registered <see cref="AuthConsumer"/>. Never logged in
/// the clear (constitution Principle IV/V).
/// </summary>
public sealed record IssuedAuthToken(
    string Value,
    string TokenType,
    DateTimeOffset ExpiresAt,
    int ExpiresInSeconds,
    IReadOnlyCollection<string> Scopes);