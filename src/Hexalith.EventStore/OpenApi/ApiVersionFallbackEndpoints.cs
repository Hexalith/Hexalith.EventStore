using Hexalith.EventStore.Contracts.Problems;
using Hexalith.EventStore.ErrorHandling;

using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.OpenApi;

/// <summary>
/// Generic gateway fallback for unsupported API versions under <c>/api/{version}/...</c>.
/// Returns documented RFC 7807 ProblemDetails responses without touching any domain pipeline:
/// <list type="bullet">
///   <item><description>Unknown route under supported <c>v1</c> returns 404 with <c>reasonCode=route-not-found</c>.</description></item>
///   <item><description>Unsupported version (e.g. <c>v2</c>) returns 400 with <c>reasonCode=unsupported-api-version</c>.</description></item>
/// </list>
/// Kept generic to EventStore; carries no domain-specific (Parties or otherwise) knowledge.
/// </summary>
public static class ApiVersionFallbackEndpoints {
    private static readonly string[] SupportedVersions = ["v1"];

    /// <summary>
    /// Maps the <c>/api/{version}/{*path}</c> fallback that emits ProblemDetails for unknown
    /// or unsupported API versions. Must be called after the concrete API endpoints are mapped
    /// so it only catches genuinely unmatched routes.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapApiVersionFallback(this IEndpointRouteBuilder endpoints) {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = endpoints
            .MapFallback("/api/{version}/{*path}", HandleApiFallback)
            .ExcludeFromDescription();

        return endpoints;
    }

    private static IResult HandleApiFallback(HttpContext context, string version) {
        ArgumentNullException.ThrowIfNull(context);

        if (string.Equals(version, "v1", StringComparison.OrdinalIgnoreCase)) {
            var notFound = new ProblemDetails {
                Type = ProblemTypeUris.NotFound,
                Title = "Not Found",
                Status = StatusCodes.Status404NotFound,
                Detail = "The requested API endpoint was not found.",
                Instance = context.Request.Path,
            };
            notFound.Extensions[GatewayProblemDetailsExtensions.ReasonCode] = "route-not-found";
            notFound.Extensions["supportedVersions"] = SupportedVersions;

            return Results.Json(notFound, statusCode: StatusCodes.Status404NotFound, contentType: "application/problem+json");
        }

        var unsupportedVersion = new ProblemDetails {
            Type = ProblemTypeUris.UnsupportedApiVersion,
            Title = "Unsupported API version",
            Status = StatusCodes.Status400BadRequest,
            Detail = $"API version '{version}' is not supported. Use one of: {string.Join(", ", SupportedVersions)}.",
            Instance = context.Request.Path,
        };
        unsupportedVersion.Extensions[GatewayProblemDetailsExtensions.ReasonCode] = "unsupported-api-version";
        unsupportedVersion.Extensions["requestedVersion"] = version;
        unsupportedVersion.Extensions["supportedVersions"] = SupportedVersions;

        return Results.Json(unsupportedVersion, statusCode: StatusCodes.Status400BadRequest, contentType: "application/problem+json");
    }
}
