using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Middleware;

public class CorrelationIdMiddleware(RequestDelegate next) {
    public const string HeaderName = "X-Correlation-ID";
    public const string HttpContextKey = "CorrelationId";

    public const int MaxIdentifierLength = 128;

    public async Task InvokeAsync(HttpContext context) {
        ArgumentNullException.ThrowIfNull(context);

        string correlationId;

        if (context.Request.Headers.TryGetValue(HeaderName, out Microsoft.Extensions.Primitives.StringValues value)
            && value.Count == 1 && value[0] is string suppliedId && IsValidIdentifier(suppliedId)) {
            correlationId = suppliedId;
        }
        else {
            correlationId = UniqueIdHelper.GenerateSortableUniqueStringId();
        }

        context.Items[HttpContextKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        // Exception handlers may clear response headers after this middleware ran. Restore the
        // validated caller correlation at the final response boundary, including safe errors.
        context.Response.OnStarting(() => {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        await next(context).ConfigureAwait(false);
    }

    /// <summary>Checks the diagnostic identifier supplied in X-Correlation-ID.</summary>
    public static bool IsValidIdentifier(string? value)
        => CorrelationIdGrammar.IsValid(value);
}
