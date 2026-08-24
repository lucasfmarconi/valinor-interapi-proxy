using ValinorInterApiProxy.Api.Contracts;

namespace ValinorInterApiProxy.Api.Middleware;

/// <summary>
/// Assigns/propagates a correlation id for every request and logs the caller's identity and
/// outcome once the response completes. Never logs token values, credentials, or statement
/// contents — only correlation id, caller subject, path, and status code (constitution
/// Principle V; FR-010).
/// </summary>
public sealed class CorrelationLoggingMiddleware(RequestDelegate next, ILogger<CorrelationLoggingMiddleware> logger)
{
    private const string CorrelationIdHeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var headerValue)
            && !string.IsNullOrWhiteSpace(headerValue)
                ? headerValue.ToString()
                : Guid.NewGuid().ToString("n");

        context.Items[ErrorResponseFactory.CorrelationIdItemKey] = correlationId;
        context.Response.Headers[CorrelationIdHeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);

            var callerSubject = context.User.FindFirst("sub")?.Value ?? "anonymous";

            logger.LogInformation(
                "Request {Method} {Path} by {Subject} completed with {StatusCode}",
                context.Request.Method,
                context.Request.Path,
                callerSubject,
                context.Response.StatusCode);
        }
    }
}

public static class CorrelationLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationLogging(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationLoggingMiddleware>();
}
