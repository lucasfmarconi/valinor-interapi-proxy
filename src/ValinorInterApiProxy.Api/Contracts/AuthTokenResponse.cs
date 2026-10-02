using System.Text.Json.Serialization;
using ValinorInterApiProxy.Core.Models;

namespace ValinorInterApiProxy.Api.Contracts;

/// <summary>
/// Consumer-facing response shape for <c>POST /auth/token</c>, mirroring OAuth2's
/// client-credentials token response (RFC 6749 §5.1) — explicit snake_case property names,
/// unlike this app's other camelCase contracts.
/// </summary>
public sealed record AuthTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn)
{
    public static AuthTokenResponse FromIssuedToken(IssuedAuthToken token) =>
        new(token.Value, token.TokenType, token.ExpiresInSeconds);
}