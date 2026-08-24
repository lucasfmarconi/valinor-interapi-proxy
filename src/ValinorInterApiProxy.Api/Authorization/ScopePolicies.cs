using Microsoft.AspNetCore.Authorization;

namespace ValinorInterApiProxy.Api.Authorization;

/// <summary>
/// A requirement satisfied when the caller's space-delimited OAuth2 <c>scope</c> claim contains
/// <see cref="RequiredScope"/>.
/// </summary>
public sealed class ScopeRequirement(string requiredScope) : IAuthorizationRequirement
{
    public string RequiredScope { get; } = requiredScope;
}

public sealed class ScopeAuthorizationHandler : AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ScopeRequirement requirement)
    {
        var scopeClaim = context.User.FindFirst("scope")?.Value;

        if (scopeClaim is not null)
        {
            var scopes = scopeClaim.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (scopes.Contains(requirement.RequiredScope, StringComparer.Ordinal))
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// The two least-privilege, endpoint-specific authorization policies (constitution Principle II;
/// FR-002/FR-003). Each proxied endpoint requires exactly one of these — never "any valid JWT".
/// </summary>
public static class ScopePolicies
{
    public const string TokenIssue = "token-issue";
    public const string ExtratoRead = "extrato-read";

    public static IServiceCollection AddScopePolicies(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, ScopeAuthorizationHandler>();

        services.AddAuthorizationBuilder()
            .AddPolicy(TokenIssue, policy => policy.Requirements.Add(new ScopeRequirement(TokenIssue)))
            .AddPolicy(ExtratoRead, policy => policy.Requirements.Add(new ScopeRequirement(ExtratoRead)));

        return services;
    }
}
