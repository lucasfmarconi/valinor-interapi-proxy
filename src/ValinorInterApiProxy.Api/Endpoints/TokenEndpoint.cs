using ValinorInterApiProxy.Api.Authorization;
using ValinorInterApiProxy.Api.Contracts;
using ValinorInterApiProxy.Core.Exceptions;
using ValinorInterApiProxy.Core.UseCases;

namespace ValinorInterApiProxy.Api.Endpoints;

/// <summary>
/// <c>POST /token</c> — issues a Banco Inter access token. Requires the <c>token-issue</c>
/// policy; the consumer never supplies Inter credentials or a certificate (spec FR-004).
/// </summary>
public static class TokenEndpoint
{
    public static IEndpointRouteBuilder MapTokenEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/token", HandleAsync)
            .RequireAuthorization(ScopePolicies.TokenIssue);

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        IssueTokenUseCase useCase,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var token = await useCase.ExecuteAsync(cancellationToken);
            return Results.Ok(TokenResponse.FromInterAccessToken(token));
        }
        catch (InterApiException)
        {
            return ErrorResponseFactory.ToResult(
                httpContext,
                StatusCodes.Status502BadGateway,
                "Banco Inter's token service is unavailable or returned an error.");
        }
    }
}
