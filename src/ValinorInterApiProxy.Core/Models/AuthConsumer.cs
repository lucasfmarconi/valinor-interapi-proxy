namespace ValinorInterApiProxy.Core.Models;

/// <summary>
/// A consumer registered to obtain self-issued JWTs from this proxy (constitution Principle II),
/// identified by a client id/secret pair and the scopes it is allowed to request.
/// </summary>
public sealed record AuthConsumer(string ClientId, string ClientSecret, IReadOnlyCollection<string> AllowedScopes);