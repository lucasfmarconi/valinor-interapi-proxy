using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ValinorInterApiProxy.Infrastructure.Auth;

namespace ValinorInterApiProxy.Infrastructure.Tests;

/// <summary>
/// Unit tests for <see cref="SymmetricJwtIssuer"/> (constitution Principle II): proves issued
/// tokens carry the expected claims and are genuinely verifiable against the same key/issuer/
/// audience, not just structurally well-formed.
/// </summary>
public sealed class SymmetricJwtIssuerTests
{
    private static readonly string SigningKeyBase64 = Convert.ToBase64String("a-test-signing-key-that-is-at-least-32-bytes-long"u8.ToArray());

    private static JwtIssuerOptions MakeOptions() => new()
    {
        SigningKey = SigningKeyBase64,
        Issuer = "valinor-interapi-proxy",
        Audience = "valinor-interapi-proxy-consumers",
        AccessTokenLifetimeMinutes = 60,
    };

    [Fact]
    public void Issue_ReturnsTokenWithExpectedClaimsAndExpiry()
    {
        var options = MakeOptions();
        var issuer = new SymmetricJwtIssuer(Options.Create(options));

        var issued = issuer.Issue("consumer-app", ["token-issue", "extrato-read"]);

        Assert.Equal("Bearer", issued.TokenType);
        Assert.Equal(["token-issue", "extrato-read"], issued.Scopes);
        Assert.InRange(issued.ExpiresInSeconds, 3590, 3600);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(issued.Value);
        Assert.Equal("consumer-app", jwt.Claims.Single(c => c.Type == "sub").Value);
        Assert.Equal("token-issue extrato-read", jwt.Claims.Single(c => c.Type == "scope").Value);
        Assert.Equal(options.Issuer, jwt.Issuer);
        Assert.Equal(options.Audience, jwt.Audiences.Single());
    }

    [Fact]
    public void Issue_TokenValidatesAgainstTheSameKeyIssuerAndAudience()
    {
        var options = MakeOptions();
        var issuer = new SymmetricJwtIssuer(Options.Create(options));
        var issued = issuer.Issue("consumer-app", ["extrato-read"]);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(options.SigningKey)),
        };

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(issued.Value, validationParameters, out _);

        Assert.Equal("consumer-app", principal.FindFirst("sub")?.Value);
    }
}