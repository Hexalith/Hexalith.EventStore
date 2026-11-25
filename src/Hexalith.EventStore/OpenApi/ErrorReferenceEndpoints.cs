using System.Net;
using System.Text.Json.Serialization;

using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.EventStore.OpenApi;

/// <summary>
/// Serves error reference documentation pages at /problems/{errorType}.
/// Each page is a simple HTML response explaining the error, with an example and resolution guidance.
/// </summary>
public static class ErrorReferenceEndpoints {
    private const string IdempotencyDocumentationUrl =
        "https://github.com/Hexalith/Hexalith.EventStore/blob/main/docs/reference/command-api.md#stable-idempotency-outcomes";

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
        string[] ResolutionSteps) {
        /// <summary>Gets additional ProblemDetails examples when several outcomes share the type URI.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<string>? AdditionalExamples { get; init; }

        /// <summary>Gets the canonical reference for details shared by related problems.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DocumentationUrl { get; init; }

        public string TypeUri => $"https://hexalith.io/problems/{Slug}";

        public string CorrectiveAction => ResolutionSteps.FirstOrDefault() ?? "Review the problem response and retry when appropriate.";
    }

    /// <summary>
    /// Gets all defined error reference models.
    /// Kept in sync with <see cref="ProblemTypeUris"/> constants.
    /// </summary>
    public static IReadOnlyList<ErrorReferenceModel> ErrorModels { get; } =
    [
        new("validation-error", "Validation Error", 400,
            "One or more fields in the command submission failed validation. The request was not processed.",
            """{"type":"https://hexalith.io/problems/validation-error","title":"Command Validation Failed","status":400,"detail":"The command has 1 validation error(s). See 'errors' for specifics.","correlationId":"01JAXYZ1234567890ABCDEFGH","tenantId":"tenant-a","errors":{"messageId":"MessageId must contain ASCII letters, digits, or hyphens and start and end with a letter or digit"}}""",
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
            "The request was malformed or contained invalid query parameters.",
            """{"type":"https://hexalith.io/problems/bad-request","title":"Bad Request","status":400,"detail":"The request body could not be parsed as valid JSON."}""",
            ["Use the response detail to correct the request body or query parameters.", "For JSON requests, verify the body matches the API schema and Content-Type is application/json."])
        {
            AdditionalExamples =
            [
                """{"type":"https://hexalith.io/problems/bad-request","title":"Bad Request","status":400,"detail":"The supplied page is invalid.","correlationId":"01JAXYZ1234567890ABCDEFGH","reasonCode":"query_invalid_page","reason":"invalid-page"}""",
            ],
        },

        new("forbidden", "Forbidden", 403,
            "The JWT is valid but the authenticated user is not authorized for the requested tenant or operation.",
            """{"type":"https://hexalith.io/problems/forbidden","title":"Forbidden","status":403,"detail":"Access denied for tenant 'tenant-b'.","correlationId":"01JAXYZ1234567890ABCDEFGH","tenantId":"tenant-b","reason":"Access denied for tenant 'tenant-b'.","reasonCode":"insufficient_permission"}""",
            ["Request access to the tenant from your administrator.", "Verify you are using the correct tenant identifier."])
        {
            AdditionalExamples =
            [
                """{"type":"https://hexalith.io/problems/forbidden","title":"Forbidden","status":403,"detail":"Forbidden","correlationId":"01JAXYZ1234567890ABCDEFGH","reasonCode":"insufficient_permission","tenantId":"tenant-b"}""",
            ],
        },

        new("not-found", "Not Found", 404,
            "Command replay could not find the requested command, or a query could not find its projection.",
            """{"type":"https://hexalith.io/problems/not-found","title":"Not Found","status":404,"detail":"No command found for correlation ID '01JAXYZ1234567890ABCDEFGH'.","correlationId":"01JAXYZ1234567890ABCDEFGH","tenantId":"tenant-a"}""",
            ["Verify the resource identifier.", "Check that the resource has not been deleted or expired."])
        {
            AdditionalExamples =
            [
                """{"type":"https://hexalith.io/problems/not-found","title":"Not Found","status":404,"detail":"The requested resource was not found.","correlationId":"01JAXYZ1234567890ABCDEFGH","reasonCode":"query_projection_missing"}""",
            ],
        },

        new("concurrency-conflict", "Concurrency Conflict", 409,
            "Another command was processed for the same aggregate entity concurrently, causing a conflict.",
            """{"type":"https://hexalith.io/problems/concurrency-conflict","title":"Conflict","status":409,"detail":"A concurrency conflict occurred. Please retry the command.","correlationId":"01JAXYZ1234567890ABCDEFGH"}""",
            ["Wait for the interval specified in the Retry-After response header.", "Retry the command — the server will resolve the conflict on the next attempt."]),

        new("command-identity-conflict", "Command Identity Conflict", 409,
            "The supplied message identifier is associated with a different or unverifiable command identity. The conflict is non-retryable with that identity tuple.",
            """{"type":"https://hexalith.io/problems/command-identity-conflict","title":"Command Identity Conflict","status":409,"detail":"The supplied MessageId cannot be verified for this command. Submit the original command identity tuple or use a new MessageId.","correlationId":"01JAXYZ1234567890ABCDEFGH"}""",
            ["Verify the original MessageId, causation identifier, and command type.", "If the original tuple is unavailable, submit the command with a new MessageId."]),

        new("idempotency-conflict", "Idempotency Conflict", 409,
            "The live tenant-scoped idempotency key is already associated with a different canonical intent.",
            """{"type":"https://hexalith.io/problems/idempotency-conflict","title":"Idempotency Conflict","status":409,"detail":"The idempotency key cannot be used for this mutation. Use the original intent or submit with a new key.","reasonCode":"idempotency_conflict","code":"idempotency_conflict","category":"idempotency_conflict","retryable":false,"correlationId":"01JAXYZ1234567890ABCDEFGH"}""",
            ["Submit the original canonical mutation intent for this key.", "Otherwise refresh state and submit with a new idempotency key."])
        {
            DocumentationUrl = IdempotencyDocumentationUrl,
        },

        new("idempotency-key-expired", "Idempotency Key Expired", 409,
            "The tenant-scoped key remains consumed, but its replay result and canonical intent digest have expired and were compacted.",
            """{"type":"https://hexalith.io/problems/idempotency-key-expired","title":"Idempotency Key Expired","status":409,"detail":"Refresh current state, then submit the intended mutation with a new idempotency key.","reasonCode":"idempotency_key_expired","code":"idempotency_key_expired","category":"idempotency_key_expired","retryable":false,"clientAction":"refresh_state_then_submit_with_new_key","correlationId":"01JAXYZ1234567890ABCDEFGH"}""",
            ["Refresh current state.", "Submit the intended mutation with a new idempotency key."])
        {
            DocumentationUrl = IdempotencyDocumentationUrl,
        },

        new("idempotency-admission-failure", "Idempotency Admission Failed", 503,
            "Trusted idempotency admission, execution fencing, or outcome reconciliation failed closed. An unavailable or unverifiable admission returns 503. An unresolved reconciliation returns 409 with a poll-first action because execution status could not be confirmed. Rejected requests return 400 and require a corrected request.",
            """{"type":"https://hexalith.io/problems/idempotency-admission-failure","title":"Idempotency Admission Failed","status":503,"detail":"Idempotency admission is temporarily unavailable. Retry later.","reasonCode":"idempotency_admission_unavailable","code":"idempotency_admission_unavailable","category":"service_unavailable","retryable":true,"clientAction":"retry_later","correlationId":"01JAXYZ1234567890ABCDEFGH"}""",
            ["Read the response's code, category, retryable, and clientAction fields to identify the exact failure — this type covers several distinct outcomes.", "For clientAction poll_status_then_retry (409), poll command status before retrying; for retry_later (503), retry after Retry-After or a short delay; for contact_support (503), contact support with the correlationId instead of retrying blindly.", "For clientAction correct_request (400), the command was rejected and will not succeed on retry; correct it and submit a new key."])
        {
            DocumentationUrl = IdempotencyDocumentationUrl,
            AdditionalExamples =
            [
                """{"type":"https://hexalith.io/problems/idempotency-admission-failure","title":"Idempotency Admission Failed","status":503,"detail":"Idempotency admission state cannot be safely interpreted.","reasonCode":"idempotency_admission_corrupt","code":"idempotency_admission_corrupt","category":"idempotency_admission_corrupt","retryable":false,"clientAction":"contact_support","correlationId":"01JAXYZ1234567890ABCDEFGH"}""",
                """{"type":"https://hexalith.io/problems/idempotency-admission-failure","title":"Idempotency Admission Failed","status":503,"detail":"Idempotency admission identity cannot be safely verified.","reasonCode":"idempotency_key_collision","code":"idempotency_key_collision","category":"idempotency_admission_corrupt","retryable":false,"clientAction":"contact_support","correlationId":"01JAXYZ1234567890ABCDEFGH"}""",
                """{"type":"https://hexalith.io/problems/idempotency-admission-failure","title":"Idempotency Admission Failed","status":503,"detail":"Legacy idempotency state is not safe to migrate automatically.","reasonCode":"idempotency_unsafe_legacy_state","code":"idempotency_unsafe_legacy_state","category":"idempotency_admission_corrupt","retryable":false,"clientAction":"contact_support","correlationId":"01JAXYZ1234567890ABCDEFGH"}""",
                """{"type":"https://hexalith.io/problems/idempotency-admission-failure","title":"Idempotency Admission Failed","status":503,"detail":"The original command status could not be refreshed. Retry later.","reasonCode":"idempotency_reconciliation_unavailable","code":"idempotency_reconciliation_unavailable","category":"service_unavailable","retryable":true,"clientAction":"retry_later","correlationId":"01JAXYZ1234567890ABCDEFGH"}""",
                """{"type":"https://hexalith.io/problems/idempotency-admission-failure","title":"Idempotency Admission Failed","status":503,"detail":"The owner could not establish current command authority.","reasonCode":"owner_admission_deferred","code":"owner_admission_deferred","category":"service_unavailable","retryable":true,"clientAction":"retry_later","correlationId":"01JAXYZ1234567890ABCDEFGH"}""",
                """{"type":"https://hexalith.io/problems/idempotency-admission-failure","title":"Idempotency Admission Failed","status":409,"detail":"The original mutation outcome remains unknown. Poll status before retrying.","reasonCode":"idempotency_outcome_unknown","code":"idempotency_outcome_unknown","category":"idempotency_outcome_unknown","retryable":true,"clientAction":"poll_status_then_retry","correlationId":"01JAXYZ1234567890ABCDEFGH"}""",
                """{"type":"https://hexalith.io/problems/idempotency-admission-failure","title":"Idempotency Admission Failed","status":400,"detail":"Idempotency admission rejected the command. Correct the request and submit a new key.","reasonCode":"idempotency_admission_rejected","code":"idempotency_admission_rejected","category":"validation_failed","retryable":false,"clientAction":"correct_request","correlationId":"01JAXYZ1234567890ABCDEFGH"}""",
            ],
        },

        new("command-correlation-ambiguous", "Ambiguous Command Correlation", 409,
            "The identifier matches multiple commands or authorized tenant scopes, so status lookup cannot select one safely.",
            """{"type":"https://hexalith.io/problems/command-correlation-ambiguous","title":"Ambiguous Command Correlation","status":409,"detail":"The identifier matches multiple commands or authorized tenant scopes. Verify the command MessageId and authorized tenant scope; contact support if a MessageId lookup remains ambiguous."}""",
            ["Verify the MessageId returned by command submission and the authorized tenant scope.", "A MessageId can still be ambiguous across authorized tenant scopes. Have an operator verify the intended tenant scope; repeating the same lookup does not resolve ambiguity.", "Do not infer command success or resubmit the business command because status lookup returned 409. Contact support with the response correlationId (the request diagnostic reference), not only the command identifier being queried."]),

        new("rate-limit-exceeded", "Rate Limit Exceeded", 429,
            "The per-tenant rate limit has been exceeded for the current time window.",
            """{"type":"https://hexalith.io/problems/rate-limit-exceeded","title":"Too Many Requests","status":429,"detail":"Rate limit exceeded for tenant 'tenant-a'. Please retry after the specified interval."}""",
            ["Wait for the interval specified in the Retry-After response header before retrying.", "Consider reducing request frequency or batching commands."]),

        new("service-unavailable", "Service Unavailable", 503,
            "The command processing pipeline is temporarily unavailable (e.g., DAPR sidecar unreachable).",
            """{"type":"https://hexalith.io/problems/service-unavailable","title":"Service Unavailable","status":503,"detail":"The command processing pipeline is temporarily unavailable. Please retry after the specified interval."}""",
            ["Retry after the Retry-After interval when present, or wait briefly before retrying.", "If the error persists, check infrastructure health."])
        {
            AdditionalExamples =
            [
                """{"type":"https://hexalith.io/problems/service-unavailable","title":"Service Unavailable","status":503,"detail":"The command processing pipeline is temporarily unavailable. Please retry after the specified interval.","correlationId":"01JAXYZ1234567890ABCDEFGH","reasonCode":"authorization_service_unavailable","retryAfter":"30"}""",
                """{"type":"https://hexalith.io/problems/service-unavailable","title":"Service Unavailable","status":503,"detail":"Catalog authority cannot safely admit this operation."}""",
            ],
        },

        new("command-status-not-found", "Command Status Not Found", 404,
            "No command status record was found for the given message or correlation identifier. The command may not have been submitted or the status may have expired.",
            """{"type":"https://hexalith.io/problems/command-status-not-found","title":"Not Found","status":404,"detail":"No command status found for message or correlation identifier '01JAXYZ1234567890ABCDEFGH'."}""",
            ["Verify the MessageId from the original 202 Accepted response.", "Status records may expire after the configured retention period."]),

        new("unsupported-api-version", "Unsupported API Version", 400,
            "The API version in the request path is not supported by this service.",
            """{"type":"https://hexalith.io/problems/unsupported-api-version","title":"Unsupported API version","status":400,"detail":"API version 'v2' is not supported. Supported versions: v1.","reasonCode":"unsupported-api-version","requestedVersion":"v2","supportedVersions":["v1"]}""",
            ["Use one of the supported API versions listed in supportedVersions.", "Update generated clients to target the documented v1 endpoints."]),

        new("domain-rejections", "Domain Rejections", 422,
            "A command reached the domain pipeline but was rejected by stable business rules or validation.",
            """{"type":"https://hexalith.io/problems/domain-rejections/party-command-validation-rejected","title":"Party Command Validation Rejected","status":422,"detail":"The command was syntactically valid but rejected by domain business rules or validation.","correlationId":"01JAXYZ1234567890ABCDEFGH","tenantId":"tenant-a","reasonCode":"party-command-validation-rejected","rejectionType":"Hexalith.Parties.Contracts.Events.PartyCommandValidationRejected","correctiveAction":"Review the rejection detail, correct the request, and retry when appropriate."}""",
            ["Open /problems/domain-rejections/{reasonCode} for the specific rejection.", "Use correctiveAction and rejectionType to route client remediation."]),

        new("backpressure-exceeded", "Backpressure Exceeded", 429,
            "The target aggregate has too many pending commands (processing or awaiting drain recovery). Per-aggregate backpressure has rejected the command to prevent saga storms.",
            """{"type":"https://hexalith.io/problems/backpressure-exceeded","title":"Too Many Requests","status":429,"detail":"The target aggregate is under backpressure due to excessive pending commands. Please retry after the specified interval.","correlationId":"01JAXYZ1234567890ABCDEFGH","tenantId":"tenant-a","domain":"orders","aggregateId":"order-1"}""",
            ["Wait for the interval specified in the Retry-After response header before retrying.", "If the error persists, the aggregate may have a large drain backlog — check system health."]),

        new("not-implemented", "Not Implemented", 501,
            "The requested operation is recognized but not yet implemented by the server.",
            """{"type":"https://hexalith.io/problems/not-implemented","title":"Not Implemented","status":501,"detail":"The requested operation is not yet implemented.","correlationId":"01JAXYZ1234567890ABCDEFGH","reasonCode":"query_not_implemented"}""",
            ["Check the API documentation for supported operations.", "This feature may be available in a future release."]),

        new("internal-server-error", "Internal Server Error", 500,
            "An unexpected server error occurred during processing.",
            """{"type":"https://hexalith.io/problems/internal-server-error","title":"Internal Server Error","status":500,"detail":"An unexpected error occurred while processing your request.","correlationId":"01JAXYZ1234567890ABCDEFGH","tenantId":"tenant-a"}""",
            ["Retry the request. Follow Retry-After when present.", "If the error persists, contact support with the correlation ID from the response."])
        {
            AdditionalExamples =
            [
                """{"type":"https://hexalith.io/problems/internal-server-error","title":"Query Failed","status":500,"detail":"Projection query execution failed.","correlationId":"01JAXYZ1234567890ABCDEFGH","reasonCode":"query_internal_error","retryAfter":"30","degradation":"projection-query-unavailable","reason":"Projection query could not be completed safely. Retry after the advised interval."}""",
            ],
        },
    ];

    private static readonly Dictionary<string, ErrorReferenceModel> _errorModels =
        ErrorModels.ToDictionary(m => m.Slug, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Maps the error reference documentation endpoints at /problems/{errorType}.
    /// </summary>
    public static IEndpointRouteBuilder MapErrorReferences(this IEndpointRouteBuilder endpoints) {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/problems")
            .RequireAuthorization(EventStoreHttpHostSecurityExtensions.HumanPolicy)
            .ExcludeFromDescription();

        _ = group.MapGet("", () => Results.Content(RenderIndexHtml(), "text/html"));

        _ = group.MapGet("/catalog.json", () => Results.Json(ErrorModels));

        _ = group.MapGet("/domain-rejections/{reasonCode}.json", (string reasonCode) =>
            Results.Json(DomainRejectionProblemCatalog.FromReasonCode(reasonCode)));

        _ = group.MapGet("/domain-rejections/{reasonCode}", (string reasonCode) => {
            DomainRejectionProblem model = DomainRejectionProblemCatalog.FromReasonCode(reasonCode);
            return Results.Content(RenderDomainRejectionHtml(model), "text/html");
        });

        _ = group.MapGet("/{errorType}", (string errorType) => {
            if (!_errorModels.TryGetValue(errorType, out ErrorReferenceModel? model)) {
                return Results.NotFound();
            }

            return Results.Content(RenderHtml(model), "text/html");
        });

        return endpoints;
    }

    private static string RenderIndexHtml() {
        string listItems = string.Join(
            "\n",
            ErrorModels.Select(static model =>
                $"<li><a href=\"/problems/{WebUtility.HtmlEncode(model.Slug)}\">{WebUtility.HtmlEncode(model.Title)}</a> <code>{WebUtility.HtmlEncode(model.TypeUri)}</code></li>"));

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="utf-8"><title>Hexalith EventStore Error Catalog</title>
            <style>body{font-family:system-ui,sans-serif;max-width:840px;margin:2rem auto;padding:0 1rem;line-height:1.6}code{background:#f4f4f4;padding:.1rem .25rem;border-radius:4px}li{margin:.4rem 0}</style>
            </head>
            <body>
            <h1>Hexalith EventStore Error Catalog</h1>
            <p>Stable ProblemDetails types, status codes, corrective actions, and examples for the v1 gateway API.</p>
            <p><strong>MVP compliance notice:</strong> Hexalith Parties is not GDPR-compliant for regulated EU personal data until v1.1. Development and API documentation surfaces intentionally keep that warning visible.</p>
            <ul>{{listItems}}</ul>
            <p>Domain-specific rejection pages are available at <code>/problems/domain-rejections/{reasonCode}</code>.</p>
            </body>
            </html>
            """;
    }

    private static string RenderDomainRejectionHtml(DomainRejectionProblem model) {
        string title = WebUtility.HtmlEncode($"{model.Title} ({model.StatusCode})");
        string typeUri = WebUtility.HtmlEncode(model.TypeUri);
        string description = WebUtility.HtmlEncode(model.Explanation);
        string correctiveAction = WebUtility.HtmlEncode(model.CorrectiveAction);
        string exampleJson = WebUtility.HtmlEncode($$"""
            {"type":"{{model.TypeUri}}","title":"{{model.Title}}","status":{{model.StatusCode}},"detail":"Domain rejection returned by the aggregate.","reasonCode":"{{model.ReasonCode}}","rejectionType":"Hexalith.Parties.Contracts.Events.{{model.RejectionName}}","correctiveAction":"{{model.CorrectiveAction}}"}
            """);

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="utf-8"><title>{{title}}</title>
            <style>body{font-family:system-ui,sans-serif;max-width:720px;margin:2rem auto;padding:0 1rem;line-height:1.6}pre{background:#f4f4f4;padding:1rem;overflow-x:auto;border-radius:4px}code{background:#f4f4f4;padding:.1rem .25rem;border-radius:4px}h1{border-bottom:2px solid #333;padding-bottom:.5rem}</style>
            </head>
            <body>
            <h1>{{title}}</h1>
            <p>{{description}}</p>
            <dl>
            <dt>Type URI</dt><dd><code>{{typeUri}}</code></dd>
            <dt>Corrective action</dt><dd>{{correctiveAction}}</dd>
            </dl>
            <h2>Example</h2>
            <pre>{{exampleJson}}</pre>
            </body>
            </html>
            """;
    }

    private static string RenderHtml(ErrorReferenceModel model) {
        string title = WebUtility.HtmlEncode($"{model.Title} ({model.StatusCode})");
        string typeUri = WebUtility.HtmlEncode(model.TypeUri);
        string description = WebUtility.HtmlEncode(model.Description);
        string correctiveAction = WebUtility.HtmlEncode(model.CorrectiveAction);
        string documentationLink = model.DocumentationUrl is null
            ? string.Empty
            : $"<p><a href=\"{WebUtility.HtmlEncode(model.DocumentationUrl)}\">Stable Idempotency Outcomes</a></p>";
        string examples = string.Join('\n', new[] { model.ExampleJson }.Concat(model.AdditionalExamples ?? [])
            .Select(example => $"<pre>{WebUtility.HtmlEncode(example)}</pre>"));

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
            {{documentationLink}}
            <dl>
            <dt>Type URI</dt><dd><code>{{typeUri}}</code></dd>
            <dt>Corrective action</dt><dd>{{correctiveAction}}</dd>
            </dl>
            <h2>Example</h2>
            {{examples}}
            <h2>Resolution</h2>
            <ol>{{resolutionItems}}</ol>
            </body>
            </html>
            """;
    }
}
