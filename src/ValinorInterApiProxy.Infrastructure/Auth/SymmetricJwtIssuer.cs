using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ValinorInterApiProxy.Core.Models;
using ValinorInterApiProxy.Core.Ports;

namespace ValinorInterApiProxy.Infrastructure.Auth;

/// <summary>
/// Signs JWTs for self-issuance to registered consumers (constitution Principle II) using a
/// symmetric key the proxy exclusively controls. Produces the short claim names
/// (<c>sub</c>, <c>scope</c>) expected by <c>ScopeAuthorizationHandler</c> and
/// <c>CorrelationLoggingMiddleware</c> in the Api layer.
/// </summary>
public sealed class SymmetricJwtIssuer(IOptions<JwtIssuerOptions> options) : IJwtIssuer
{
    private static readonly JwtSecurityTokenHandler Handler = new();

    public IssuedAuthToken Issue(string subject, IReadOnlyCollection<string> scopes)
    {
        var settings = options.Value;
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(settings.AccessTokenLifetimeMinutes);

        var signingKey = new SymmetricSecurityKey(Convert.FromBase64String(settings.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", subject),
            new("scope", string.Join(' ', scopes)),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
        };

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var value = Handler.WriteToken(token);
        var expiresInSeconds = (int)(expiresAt - now).TotalSeconds;

        return new IssuedAuthToken(value, "Bearer", expiresAt, expiresInSeconds, scopes);
    }
}