using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ValinorInterApiProxy.Api.Tests.Infrastructure;

/// <summary>
/// Stands in for JWT bearer authentication in tests: reads the caller identity and scope from
/// plain headers instead of validating a signed token, so authorization-policy tests can drive
/// the <c>token-issue</c> / <c>extrato-read</c> scope checks without a real identity provider.
/// Absence of <see cref="SubjectHeader"/> mirrors "no JWT" and yields the default 401 challenge.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestScheme";
    public const string SubjectHeader = "Test-Subject";
    public const string ScopeHeader = "Test-Scope";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(SubjectHeader, out var subject) || string.IsNullOrEmpty(subject))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim> { new("sub", subject!) };
        if (Request.Headers.TryGetValue(ScopeHeader, out var scope) && !string.IsNullOrEmpty(scope))
        {
            claims.Add(new Claim("scope", scope!));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
