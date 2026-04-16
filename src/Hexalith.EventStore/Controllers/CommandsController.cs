using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.Models;
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
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> Submit([FromBody] SubmitCommandRequest request, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();

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

        return Accepted(new SubmitCommandResponse(result.CorrelationId));
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
