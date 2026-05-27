using System.Security.Claims;
using System.Text.Json;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Validation;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/commands")]
[Consumes("application/json")]
[Tags("Commands")]
public class CommandsController(IMediator mediator, ExtensionMetadataSanitizer extensionSanitizer, ILogger<CommandsController> logger) : ControllerBase {
    private const string GlobalAdminExtensionKey = "actor:globalAdmin";

    // ES-1 DoS guard for the optional domain-service result payload.
    // The length cap is the real protection: the result string comes from the domain-service
    // round-trip (SubmitCommandResult.ResultPayload), NOT the inbound HTTP body, so the
    // [RequestSizeLimit(1_048_576)] on Submit does not bound it. The cap matches the
    // DaprInfrastructureQueryService 64 KiB precedent. MaxDepth = 64 surfaces JsonDocument's
    // implicit default depth in source (mirroring AdminStreamQueryController._payloadParseOptions);
    // over-depth input still throws JsonException and is handled by the existing catch below.
    private const int MaxResultPayloadCharacters = 64 * 1024;
    private static readonly JsonDocumentOptions _payloadParseOptions = new() { MaxDepth = 64 };

    /// <summary>
    /// Submits a command for asynchronous processing.
    /// </summary>
    /// <remarks>
    /// The command is validated and routed to the appropriate domain aggregate for processing.
    /// On success, returns 202 Accepted with a Location header pointing to the status polling endpoint.
    /// The consumer should poll the status endpoint using the correlation ID until a terminal status is reached.
    /// </remarks>
    /// <response code="202">Command accepted for processing. Check status at the Location header URL.</response>
    /// <response code="400">Validation failed. See errors object for field-level details.</response>
    /// <response code="401">Authentication required. Provide a valid JWT Bearer token.</response>
    /// <response code="403">Forbidden. Valid JWT but not authorized for the requested tenant.</response>
    /// <response code="404">Not found. The specified domain or aggregate does not exist.</response>
    /// <response code="409">Concurrency conflict. Retry after the interval in the Retry-After header.</response>
    /// <response code="422">Unprocessable entity. The command was rejected by domain business rules.</response>
    /// <response code="429">Rate limit exceeded. Retry after the Retry-After interval.</response>
    /// <response code="503">Service unavailable. The processing pipeline is temporarily down.</response>
    [HttpPost]
    [RequestSizeLimit(1_048_576)]
    [ProducesResponseType(typeof(SubmitCommandResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> Submit([FromBody] SubmitCommandRequest request, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? UniqueIdHelper.GenerateSortableUniqueStringId();

        // Store tenant in HttpContext for error handlers and rate limiter OnRejected callback
        if (!string.IsNullOrEmpty(request.Tenant)) {
            HttpContext.Items["RequestTenantId"] = request.Tenant;
        }

        // Extract UserId from JWT -- use 'sub' claim ONLY (F-RT2: 'name' may be user-controllable)
        string? userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(userId)) {
            logger.LogWarning(
                "JWT 'sub' claim missing for command submission. Rejecting request as unauthorized. CorrelationId={CorrelationId}.",
                correlationId);

            return Unauthorized();
        }

        // SEC-4: Extension metadata sanitization at API gateway
        SanitizeResult sanitizeResult = extensionSanitizer.Sanitize(request.Extensions);
        if (!sanitizeResult.IsSuccess) {
            logger.LogWarning(
                "Security event: SecurityEvent={SecurityEvent}, CorrelationId={CorrelationId}, Tenant={TenantId}, Domain={Domain}, Reason={Reason}",
                "ExtensionMetadataRejected",
                correlationId,
                request.Tenant,
                request.Domain,
                sanitizeResult.RejectionReason);

            ProblemDetails problemDetails = ValidationProblemDetailsFactory.Create(
                sanitizeResult.RejectionReason!,
                new Dictionary<string, string> { ["extensions"] = sanitizeResult.RejectionReason! },
                correlationId,
                request.Tenant);
            problemDetails.Instance = HttpContext?.Request.Path;

            var sanitizationResponse = new ObjectResult(problemDetails) { StatusCode = StatusCodes.Status400BadRequest };
            sanitizationResponse.ContentTypes.Add("application/problem+json");
            return sanitizationResponse;
        }

        Dictionary<string, string>? extensions = BuildTrustedExtensions(request.Extensions);

        var command = new SubmitCommand(
            MessageId: request.MessageId,
            Tenant: request.Tenant,
            Domain: request.Domain,
            AggregateId: request.AggregateId,
            CommandType: request.CommandType,
            Payload: JsonSerializer.SerializeToUtf8Bytes(request.Payload),
            CorrelationId: string.IsNullOrWhiteSpace(request.CorrelationId) ? request.MessageId : request.CorrelationId,
            UserId: userId,
            Extensions: extensions,
            IsGlobalAdmin: IsGlobalAdministrator(User));

        SubmitCommandResult result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        // RFC 7231: Location header should be absolute URI
        string absoluteLocationUri = $"{Request.Scheme}://{Request.Host}/api/v1/commands/status/{result.CorrelationId}";
        Response.Headers["Location"] = absoluteLocationUri;
        Response.Headers["Retry-After"] = "1";

        return Accepted(new SubmitCommandResponse(result.CorrelationId, ParseOptionalResultPayload(result.ResultPayload, result.CorrelationId, logger)));
    }

    private static JsonElement? ParseOptionalResultPayload(string? resultPayload, string correlationId, ILogger logger) {
        if (string.IsNullOrWhiteSpace(resultPayload)) {
            return null;
        }

        // DoS guard: reject oversized result payloads before parsing so a hostile or buggy
        // domain service cannot stall the request thread / consume large memory in JsonDocument.Parse.
        // resultPayload.Length is a cheap O(1) UTF-16 char count; since UTF-8 byte length is always
        // >= the UTF-16 char count, this cap also bounds the byte size. Lengths are logged as numbers
        // only -- never the payload content (Rule 5 / NFR12).
        if (resultPayload.Length > MaxResultPayloadCharacters) {
            logger.LogWarning(
                "Result payload from domain service for correlation '{CorrelationId}' exceeds the {MaxLength}-character parse cap (observed {ObservedLength}); returning no resultPayload.",
                correlationId,
                MaxResultPayloadCharacters,
                resultPayload.Length);
            return null;
        }

        try {
            using var document = JsonDocument.Parse(resultPayload, _payloadParseOptions);
            JsonElement element = document.RootElement.Clone();
            return element.ValueKind == JsonValueKind.Undefined ? null : element;
        }
        catch (JsonException) {
            logger.LogWarning(
                "Malformed result payload from domain service for correlation '{CorrelationId}' could not be parsed as JSON; returning no resultPayload.",
                correlationId);
            return null;
        }
    }

    private Dictionary<string, string>? BuildTrustedExtensions(IDictionary<string, string>? requestExtensions) {
        if (requestExtensions is null || requestExtensions.Count == 0) {
            return null;
        }

        var trustedExtensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> extension in requestExtensions) {
            if (string.Equals(extension.Key, GlobalAdminExtensionKey, StringComparison.OrdinalIgnoreCase)) {
                logger.LogWarning(
                    "Reserved extension metadata key '{ExtensionKey}' was ignored for command submission.",
                    extension.Key);
                continue;
            }

            trustedExtensions[extension.Key] = extension.Value;
        }

        return trustedExtensions.Count > 0 ? trustedExtensions : null;
    }

    private static bool IsGlobalAdministrator(ClaimsPrincipal principal)
        => GlobalAdministratorHelper.IsGlobalAdministrator(principal);

}
