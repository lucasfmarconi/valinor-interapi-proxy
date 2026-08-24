using ValinorInterApiProxy.Api.Authorization;
using ValinorInterApiProxy.Api.Contracts;
using ValinorInterApiProxy.Core.Exceptions;
using ValinorInterApiProxy.Core.Models;
using ValinorInterApiProxy.Core.UseCases;

namespace ValinorInterApiProxy.Api.Endpoints;

/// <summary>
/// <c>GET /extrato</c> — retrieves a Banco Inter bank statement for a date range. Requires the
/// <c>extrato-read</c> policy; never requires a prior <c>/token</c> call (spec FR-005).
/// </summary>
public static class ExtratoEndpoint
{
    public static IEndpointRouteBuilder MapExtratoEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/extrato", HandleAsync)
            .RequireAuthorization(ScopePolicies.ExtratoRead);

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        DateOnly startDate,
        DateOnly endDate,
        GetStatementUseCase useCase,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var statement = await useCase.ExecuteAsync(new StatementQuery(startDate, endDate), cancellationToken);
            return Results.Ok(StatementResponse.FromBankStatement(statement));
        }
        catch (StatementValidationException ex)
        {
            return ErrorResponseFactory.ToResult(httpContext, StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (InterApiException)
        {
            return ErrorResponseFactory.ToResult(
                httpContext,
                StatusCodes.Status502BadGateway,
                "Banco Inter's statement service is unavailable or returned an error.");
        }
    }
}
