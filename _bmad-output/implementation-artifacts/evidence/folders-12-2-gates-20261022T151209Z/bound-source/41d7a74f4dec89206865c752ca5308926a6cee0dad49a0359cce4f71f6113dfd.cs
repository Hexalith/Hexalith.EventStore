namespace Hexalith.EventStore.Admin.Server.Host.Middleware;

/// <summary>
/// Adds a correlation ID to the current request/response for traceability.
/// </summary>
public class CorrelationIdMiddleware(RequestDelegate next) {
    public const string HeaderName = "X-Correlation-ID";
    public const string HttpContextKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context) {
        ArgumentNullException.ThrowIfNull(context);

        string correlationId;

        if (context.Request.Headers.TryGetValue(HeaderName, out Microsoft.Extensions.Primitives.StringValues value)
            && Guid.TryParse(value.ToString(), out _)) {
            correlationId = value.ToString();
        }
        else {
            correlationId = Guid.NewGuid().ToString();
        }

        context.Items[HttpContextKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        await next(context).ConfigureAwait(false);
    }
}