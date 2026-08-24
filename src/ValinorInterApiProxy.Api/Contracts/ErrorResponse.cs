namespace ValinorInterApiProxy.Api.Contracts;

/// <summary>
/// Consumer-safe error shape returned for both JWT-auth failures and Inter upstream failures.
/// Matches the <c>{ correlationId, message }</c> schema in <c>contracts/token.openapi.yaml</c>
/// and <c>contracts/extrato.openapi.yaml</c>. Never carries credentials, tokens, certificate
/// material, or Inter's internal error details (FR-008/FR-009).
/// </summary>
public sealed record ErrorResponse(string CorrelationId, string Message);

/// <summary>
/// Builds consumer-safe <see cref="ErrorResponse"/> instances, sourcing the correlation id
/// recorded by <c>CorrelationLoggingMiddleware</c> for the current request.
/// </summary>
public static class ErrorResponseFactory
{
    public static ErrorResponse Create(HttpContext context, string message) =>
        new(GetCorrelationId(context), message);

    public static IResult ToResult(HttpContext context, int statusCode, string message) =>
        Results.Json(Create(context, message), statusCode: statusCode);

    private static string GetCorrelationId(HttpContext context) =>
        context.Items.TryGetValue(CorrelationIdItemKey, out var value) && value is string correlationId
            ? correlationId
            : context.TraceIdentifier;

    internal const string CorrelationIdItemKey = "CorrelationId";
}
