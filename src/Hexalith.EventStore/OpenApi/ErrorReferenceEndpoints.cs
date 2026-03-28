using System.Net;

using Hexalith.EventStore.ErrorHandling;

namespace Hexalith.EventStore.OpenApi;

/// <summary>
/// Serves error reference documentation pages at /problems/{errorType}.
/// Each page is a simple HTML response explaining the error, with an example and resolution guidance.
/// </summary>
public static class ErrorReferenceEndpoints {
    /// <summary>
    /// Data model for an error reference page.
    /// </summary>
    /// <param name="Slug">The error type slug (e.g., "validation-error").</param>
    /// <param name="Title">Human-readable error title.</param>
    /// <param name="StatusCode">The HTTP status code associated with the error.</param>
    /// <param name="Description">Human-readable description of when this error occurs.</param>
    /// <param name="ExampleJson">Example ProblemDetails JSON response.</param>
    /// <param name="ResolutionSteps">Ordered resolution steps for the consumer.</param>
    public record ErrorReferenceModel(
        string Slug,
        string Title,
        int StatusCode,
        string Description,
        string ExampleJson,
        string[] ResolutionSteps);

    /// <summary>
    /// Gets all defined error reference models.
    /// Kept in sync with <see cref="ProblemTypeUris"/> constants.
    /// </summary>
    public static IReadOnlyList<ErrorReferenceModel> ErrorModels { get; } =
    [
        new("validation-error", "Validation Error", 400,
            "One or more fields in the command submission failed validation. The request was not processed.",
            """{"type":"https://hexalith.io/problems/validation-error","title":"Validation Error","status":400,"detail":"The command has 1 validation error(s). See 'errors' for specifics.","errors":{"messageId":"'messageId' must be a valid 26-character ULID."}}""",
            ["Check the 'errors' object for specific field failures.", "Correct the invalid fields and resubmit."]),

        new("authentication-required", "Authentication Required", 401,
            "No JWT was provided or the JWT has an invalid signature or issuer.",
            """{"type":"https://hexalith.io/problems/authentication-required","title":"Unauthorized","status":401,"detail":"Authentication is required to access this resource."}""",
            ["Obtain a valid JWT from your identity provider.", "Include the token in the Authorization header: Bearer {token}."]),

        new("token-expired", "Token Expired", 401,
            "The JWT provided has expired and is no longer valid.",
            """{"type":"https://hexalith.io/problems/token-expired","title":"Unauthorized","status":401,"detail":"The provided token has expired."}""",
            ["Refresh your token using your identity provider's token refresh endpoint.", "Retry the request with the new token."]),

        new("bad-request", "Bad Request", 400,
            "The request body is malformed and does not match the expected JSON format.",
            """{"type":"https://hexalith.io/problems/bad-request","title":"Bad Request","status":400,"detail":"The request body could not be parsed as valid JSON."}""",
            ["Verify the JSON structure matches the API schema.", "Ensure Content-Type is application/json."]),

        new("forbidden", "Forbidden", 403,
            "The JWT is valid but the authenticated user is not authorized for the requested tenant or operation.",
            """{"type":"https://hexalith.io/problems/forbidden","title":"Forbidden","status":403,"detail":"Access denied for tenant 'tenant-b'."}""",
            ["Request access to the tenant from your administrator.", "Verify you are using the correct tenant identifier."]),

        new("not-found", "Not Found", 404,
            "The requested resource does not exist.",
            """{"type":"https://hexalith.io/problems/not-found","title":"Not Found","status":404,"detail":"No command found for correlation ID '01JAXYZ1234567890ABCDEFGH'."}""",
            ["Verify the resource identifier.", "Check that the resource has not been deleted or expired."]),

        new("concurrency-conflict", "Concurrency Conflict", 409,
            "Another command was processed for the same aggregate entity concurrently, causing a conflict.",
            """{"type":"https://hexalith.io/problems/concurrency-conflict","title":"Conflict","status":409,"detail":"A concurrency conflict occurred. Please retry after the specified interval."}""",
            ["Wait for the interval specified in the Retry-After response header.", "Retry the command — the server will resolve the conflict on the next attempt."]),

        new("rate-limit-exceeded", "Rate Limit Exceeded", 429,
            "The per-tenant rate limit has been exceeded for the current time window.",
            """{"type":"https://hexalith.io/problems/rate-limit-exceeded","title":"Too Many Requests","status":429,"detail":"Rate limit exceeded for tenant 'tenant-a'. Please retry after the specified interval."}""",
            ["Wait for the interval specified in the Retry-After response header before retrying.", "Consider reducing request frequency or batching commands."]),

        new("service-unavailable", "Service Unavailable", 503,
            "The command processing pipeline is temporarily unavailable (e.g., DAPR sidecar unreachable).",
            """{"type":"https://hexalith.io/problems/service-unavailable","title":"Service Unavailable","status":503,"detail":"The command processing service is temporarily unavailable. Please retry after the specified interval."}""",
            ["Retry after the Retry-After interval (typically 30 seconds).", "If the error persists, check infrastructure health."]),

        new("command-status-not-found", "Command Status Not Found", 404,
            "No command status record was found for the given correlation ID. The command may not have been submitted or the status may have expired.",
            """{"type":"https://hexalith.io/problems/command-status-not-found","title":"Not Found","status":404,"detail":"No command status found for correlation ID '01JAXYZ1234567890ABCDEFGH'."}""",
            ["Verify the correlation ID from the original 202 Accepted response.", "Status records may expire after the configured retention period."]),

        new("backpressure-exceeded", "Backpressure Exceeded", 429,
            "The target aggregate has too many pending commands (processing or awaiting drain recovery). Per-aggregate backpressure has rejected the command to prevent saga storms.",
            """{"type":"https://hexalith.io/problems/backpressure-exceeded","title":"Too Many Requests","status":429,"detail":"The target aggregate is under backpressure due to excessive pending commands. Please retry after the specified interval.","correlationId":"01JAXYZ1234567890ABCDEFGH","tenantId":"tenant-a"}""",
            ["Wait for the interval specified in the Retry-After response header before retrying.", "If the error persists, the aggregate may have a large drain backlog — check system health."]),

        new("not-implemented", "Not Implemented", 501,
            "The requested operation is recognized but not yet implemented by the server.",
            """{"type":"https://hexalith.io/problems/not-implemented","title":"Not Implemented","status":501,"detail":"The requested operation is not yet implemented."}""",
            ["Check the API documentation for supported operations.", "This feature may be available in a future release."]),

        new("internal-server-error", "Internal Server Error", 500,
            "An unexpected server error occurred during processing.",
            """{"type":"https://hexalith.io/problems/internal-server-error","title":"Internal Server Error","status":500,"detail":"An unexpected error occurred. Please retry or contact support."}""",
            ["Retry the command.", "If the error persists, contact support with the correlation ID from the response."]),
    ];

    private static readonly Dictionary<string, ErrorReferenceModel> _errorModels =
        ErrorModels.ToDictionary(m => m.Slug, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Maps the error reference documentation endpoints at /problems/{errorType}.
    /// </summary>
    public static IEndpointRouteBuilder MapErrorReferences(this IEndpointRouteBuilder endpoints) {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/problems")
            .ExcludeFromDescription();

        _ = group.MapGet("/{errorType}", (string errorType) => {
            if (!_errorModels.TryGetValue(errorType, out ErrorReferenceModel? model)) {
                return Results.NotFound();
            }

            return Results.Content(RenderHtml(model), "text/html");
        });

        return endpoints;
    }

    private static string RenderHtml(ErrorReferenceModel model) {
        string title = WebUtility.HtmlEncode($"{model.Title} ({model.StatusCode})");
        string description = WebUtility.HtmlEncode(model.Description);
        string exampleJson = WebUtility.HtmlEncode(model.ExampleJson);

        string resolutionItems = string.Join(
            "\n",
            model.ResolutionSteps.Select(s => $"<li>{WebUtility.HtmlEncode(s)}</li>"));

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="utf-8"><title>{{title}}</title>
            <style>body{font-family:system-ui,sans-serif;max-width:720px;margin:2rem auto;padding:0 1rem;line-height:1.6}pre{background:#f4f4f4;padding:1rem;overflow-x:auto;border-radius:4px}h1{border-bottom:2px solid #333;padding-bottom:.5rem}</style>
            </head>
            <body>
            <h1>{{title}}</h1>
            <p>{{description}}</p>
            <h2>Example</h2>
            <pre>{{exampleJson}}</pre>
            <h2>Resolution</h2>
            <ol>{{resolutionItems}}</ol>
            </body>
            </html>
            """;
    }
}
