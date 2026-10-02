using ValinorInterApiProxy.Api.Contracts;
using ValinorInterApiProxy.Core.Exceptions;
using ValinorInterApiProxy.Core.UseCases;

namespace ValinorInterApiProxy.Api.Endpoints;

/// <summary>
/// <c>POST /auth/token</c> — self-issues a JWT to a registered consumer in exchange for its
/// client id/secret (constitution Principle II). The one non-proxied, anonymous endpoint
/// (constitution Principle III) — it never calls Banco Inter.
/// </summary>
public static class AuthTokenEndpoint
{
    public static IEndpointRouteBuilder MapAuthTokenEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/auth/token", HandleAsync)
            .AllowAnonymous();

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        IssueAuthTokenUseCase useCase,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return ErrorResponseFactory.ToResult(
                httpContext,
                StatusCodes.Status400BadRequest,
                "Request body must be application/x-www-form-urlencoded.");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var clientId = form["client_id"].ToString();
        var clientSecret = form["client_secret"].ToString();
        var scope = form["scope"].ToString();

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            return ErrorResponseFactory.ToResult(
                httpContext,
                StatusCodes.Status400BadRequest,
                "client_id and client_secret are required.");
        }

        try
        {
            var token = useCase.Execute(clientId, clientSecret, scope);
            return Results.Ok(AuthTokenResponse.FromIssuedToken(token));
        }
        catch (InvalidClientCredentialsException)
        {
            return ErrorResponseFactory.ToResult(
                httpContext,
                StatusCodes.Status401Unauthorized,
                "Invalid client credentials.");
        }
        catch (InvalidScopeRequestException ex)
        {
            return ErrorResponseFactory.ToResult(httpContext, StatusCodes.Status400BadRequest, ex.Message);
        }
    }
}