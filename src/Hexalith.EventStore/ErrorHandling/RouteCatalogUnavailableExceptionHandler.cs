using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.ErrorHandling;

/// <summary>Returns a bounded refusal without disclosing catalog, target or resource existence.</summary>
public sealed class RouteCatalogUnavailableExceptionHandler : IExceptionHandler
{
    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        if (exception is not RouteCatalogUnavailableException) return false;
        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "Service Unavailable",
            Detail = "Catalog authority cannot safely admit this operation.",
            Type = ProblemTypeUris.ServiceUnavailable,
        }, options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json", cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }
}
