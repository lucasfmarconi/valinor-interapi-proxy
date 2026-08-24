using ValinorInterApiProxy.Core.Models;

namespace ValinorInterApiProxy.Api.Contracts;

/// <summary>
/// Consumer-facing response shape for <c>POST /token</c>, matching
/// <c>contracts/token.openapi.yaml</c>'s <c>TokenResponse</c> schema.
/// </summary>
public sealed record TokenResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAt)
{
    public static TokenResponse FromInterAccessToken(InterAccessToken token) =>
        new(token.Value, token.TokenType, token.ExpiresAt);
}
