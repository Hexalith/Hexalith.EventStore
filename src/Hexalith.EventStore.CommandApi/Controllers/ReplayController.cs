
using System.Diagnostics;

using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.CommandApi.Models;
using Hexalith.EventStore.CommandApi.Telemetry;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Telemetry;

using MediatR;

using Hexalith.EventStore.CommandApi.ErrorHandling;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.CommandApi.Controllers;
/// <summary>
/// Controller for replaying previously failed commands.
/// Route: POST /api/v1/commands/replay/{correlationId}
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/commands/replay")]
[Consumes("application/json")]
[Tags("Commands")]
public class ReplayController(
    ICommandArchiveStore archiveStore,
    ICommandStatusStore statusStore,
    IMediator mediator,
    ILogger<ReplayController> logger) : ControllerBase {
    private static readonly HashSet<CommandStatus> _replayableStatuses =
    [
        CommandStatus.Rejected,
        CommandStatus.PublishFailed,
        CommandStatus.TimedOut,
    ];

    /// <summary>
    /// Replays a previously failed command by correlation ID.
    /// </summary>
    /// <remarks>
    /// Retrieves the archived command and resubmits it through the full processing pipeline
    /// with a new correlation ID. Only commands in terminal failure states (Rejected, PublishFailed,
    /// TimedOut) can be replayed.
    /// </remarks>
    /// <response code="202">Replay accepted. Check status at the Location header URL.</response>
    /// <response code="400">Invalid correlation ID format.</response>
    /// <response code="401">Authentication required. Provide a valid JWT Bearer token.</response>
    /// <response code="403">Forbidden. No tenant authorization claims found.</response>
    /// <response code="404">No archived command found for the given correlation ID.</response>
    /// <response code="409">Conflict. Command is not in a replayable state.</response>
    /// <response code="429">Rate limit exceeded. Retry after the Retry-After interval.</response>
    /// <response code="500">Internal server error. Archived command data is corrupted.</response>
    [HttpPost("{correlationId}")]
    [RequestSizeLimit(1_048_576)]
    [ProducesResponseType(typeof(ReplayCommandResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> Replay(string correlationId, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(correlationId);

        using Activity? activity = EventStoreActivitySources.CommandApi.StartActivity(
            EventStoreActivitySources.Replay, ActivityKind.Server);
        _ = (activity?.SetTag(EventStoreActivitySource.TagCorrelationId, correlationId));

        string requestCorrelationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? correlationId;

        try {
            // Validate GUID format for path parameter
            if (!Guid.TryParse(correlationId, out _)) {
                _ = (activity?.SetStatus(ActivityStatusCode.Error, "InvalidCorrelationId"));
                return CreateProblemDetails(
                    StatusCodes.Status400BadRequest,
                    ProblemTypeUris.BadRequest,
                    "Bad Request",
                    $"Correlation ID '{correlationId}' is not a valid GUID format.",
                    requestCorrelationId);
            }

            // Store correlationId in HttpContext.Items for error handler access
            HttpContext.Items["ReplayCorrelationId"] = correlationId;

            // Extract tenant claims (same pattern as Story 2.5/2.6)
            var tenantClaims = User.FindAll("eventstore:tenant")
                .Select(c => c.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();

            // AC #6: 403 for missing tenant claims
            if (tenantClaims.Count == 0) {
                logger.LogWarning(
                    "Replay denied: no tenant claims. CorrelationId={CorrelationId}",
                    requestCorrelationId);

                _ = (activity?.SetStatus(ActivityStatusCode.Error, "NoTenantClaims"));
                return CreateProblemDetails(
                    StatusCodes.Status403Forbidden,
                    ProblemTypeUris.Forbidden,
                    "Forbidden",
                    "No tenant authorization claims found. Access denied.",
                    requestCorrelationId);
            }

            // AC #5: Search for archived command across authorized tenants (SEC-3)
            ArchivedCommand? archivedCommand = null;
            string? foundTenant = null;

            foreach (string tenant in tenantClaims) {
                _ = (activity?.SetTag(EventStoreActivitySource.TagTenantId, tenant));

                archivedCommand = await archiveStore
                    .ReadCommandAsync(tenant, correlationId, cancellationToken)
                    .ConfigureAwait(false);

                if (archivedCommand is not null) {
                    foundTenant = tenant;
                    break;
                }
            }

            // AC #4: 404 for non-existent or expired correlation ID
            if (archivedCommand is null || foundTenant is null) {
                logger.LogDebug(
                    "Archived command not found: CorrelationId={CorrelationId}, TenantsSearched={Tenants}",
                    correlationId,
                    string.Join(",", tenantClaims));

                _ = (activity?.SetStatus(ActivityStatusCode.Error, "NotFound"));
                return CreateProblemDetails(
                    StatusCodes.Status404NotFound,
                    ProblemTypeUris.NotFound,
                    "Not Found",
                    $"No command found for correlation ID '{correlationId}'.",
                    requestCorrelationId);
            }

            // Store tenantId in HttpContext.Items for error handler access
            HttpContext.Items["RequestTenantId"] = foundTenant;

            // AC #3: Read current status to validate replayability
            CommandStatusRecord? statusRecord = await statusStore
                .ReadStatusAsync(foundTenant, correlationId, cancellationToken)
                .ConfigureAwait(false);

            // H5: Null status (expired or never written) -- cannot determine replayability
            if (statusRecord is null) {
                _ = (activity?.SetStatus(ActivityStatusCode.Error, "Conflict"));
                return CreateConflictProblemDetails(
                    "Unknown",
                    $"Status tracking for command '{correlationId}' has expired. Cannot determine replayability. Replay is permitted only for commands with terminal failure status (Rejected, PublishFailed, TimedOut).",
                    requestCorrelationId);
            }

            // Check if status is replayable
            if (!_replayableStatuses.Contains(statusRecord.Status)) {
                string detail = statusRecord.Status == CommandStatus.Completed
                    ? $"Command '{correlationId}' has already completed successfully. Replay is not permitted for completed commands. Replay is permitted only for commands with terminal failure status (Rejected, PublishFailed, TimedOut)."
                    : $"Command '{correlationId}' is currently in-flight (status: {statusRecord.Status}). Wait for processing to complete or time out before replaying. Replay is permitted only for commands with terminal failure status (Rejected, PublishFailed, TimedOut).";

                _ = (activity?.SetStatus(ActivityStatusCode.Error, "Conflict"));
                return CreateConflictProblemDetails(
                    statusRecord.Status.ToString(),
                    detail,
                    requestCorrelationId);
            }

            string previousStatus = statusRecord.Status.ToString();

            // Generate a new correlation ID for replay tracking
            string replayCorrelationId = Guid.NewGuid().ToString();

            // AC #1, #2: Create SubmitCommand from archived data and send through full MediatR pipeline
            SubmitCommand command;
            try {
                command = archivedCommand.ToSubmitCommand(replayCorrelationId, foundTenant);
            }
            catch (InvalidOperationException ex) {
                logger.LogError(
                    ex,
                    "Corrupted archived command: CorrelationId={CorrelationId}, TenantId={TenantId}",
                    correlationId,
                    foundTenant);

                _ = (activity?.SetStatus(ActivityStatusCode.Error, "CorruptedArchive"));
                return CreateProblemDetails(
                    StatusCodes.Status500InternalServerError,
                    ProblemTypeUris.InternalServerError,
                    "Internal Server Error",
                    $"Stored command data for '{correlationId}' is invalid and cannot be replayed.",
                    requestCorrelationId);
            }

            logger.LogInformation(
                "Replay initiated: CorrelationId={CorrelationId}, TenantId={TenantId}, PreviousStatus={PreviousStatus}, IsReplay={IsReplay}",
                replayCorrelationId,
                foundTenant,
                previousStatus,
                true);

            _ = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

            // AC #1: Return 202 Accepted with replay response
            string absoluteLocationUri = $"{Request.Scheme}://{Request.Host}/api/v1/commands/status/{replayCorrelationId}";
            Response.Headers["Location"] = absoluteLocationUri;
            Response.Headers["Retry-After"] = "1";

            _ = (activity?.SetStatus(ActivityStatusCode.Ok));
            return Accepted(absoluteLocationUri, new ReplayCommandResponse(replayCorrelationId, IsReplay: true, PreviousStatus: previousStatus, OriginalCorrelationId: correlationId));
        }
        catch (Exception ex) {
            _ = (activity?.AddException(ex));
            _ = (activity?.SetStatus(ActivityStatusCode.Error, ex.Message));
            throw;
        }
    }

    private ObjectResult CreateProblemDetails(int statusCode, string type, string title, string detail, string correlationId) {
        var problemDetails = new ProblemDetails {
            Status = statusCode,
            Title = title,
            Type = type,
            Detail = detail,
            Instance = HttpContext?.Request.Path,
            Extensions =
            {
                ["correlationId"] = correlationId,
            },
        };

        if (HttpContext is not null) {
            Response.ContentType = "application/problem+json";
        }

        return new ObjectResult(problemDetails) { StatusCode = statusCode };
    }

    private ObjectResult CreateConflictProblemDetails(string currentStatus, string detail, string requestCorrelationId) {
        var problemDetails = new ProblemDetails {
            Status = StatusCodes.Status409Conflict,
            Title = "Conflict",
            Type = ProblemTypeUris.ConcurrencyConflict,
            Detail = detail,
            Instance = HttpContext?.Request.Path,
            Extensions =
            {
                ["correlationId"] = requestCorrelationId,
                ["currentStatus"] = currentStatus,
            },
        };

        if (HttpContext is not null) {
            Response.ContentType = "application/problem+json";
        }

        return new ObjectResult(problemDetails) { StatusCode = StatusCodes.Status409Conflict };
    }
}
