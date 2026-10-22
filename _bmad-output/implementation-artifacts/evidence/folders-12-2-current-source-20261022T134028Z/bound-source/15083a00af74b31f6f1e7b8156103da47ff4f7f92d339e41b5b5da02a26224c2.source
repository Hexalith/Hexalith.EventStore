
using System.Diagnostics;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.Models;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Diagnostics;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Telemetry;
using Hexalith.EventStore.Telemetry;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.Controllers;
/// <summary>
/// Controller for replaying previously failed commands.
/// Route: POST /api/v1/commands/replay/{correlationId}
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/commands/replay")]
[Tags("Commands")]
public class ReplayController(
    ICommandArchiveStore archiveStore,
    ICommandStatusStore statusStore,
    IMediator mediator,
    ILogger<ReplayController> logger,
    ICommandCorrelationIndex? correlationIndex = null) : ControllerBase {
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

        using Activity? activity = EventStoreActivitySources.EventStore.StartActivity(
            EventStoreActivitySources.Replay, ActivityKind.Server);
        _ = (activity?.SetTag(EventStoreActivitySource.TagCorrelationId, correlationId));

        string requestCorrelationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? correlationId;

        try {
            if (!CorrelationIdMiddleware.IsValidIdentifier(correlationId)) {
                _ = (activity?.SetStatus(ActivityStatusCode.Error, "InvalidCorrelationId"));
                return CreateProblemDetails(
                    StatusCodes.Status400BadRequest,
                    ProblemTypeUris.BadRequest,
                    "Bad Request",
                    $"Correlation ID must be 1-{CorrelationIdMiddleware.MaxIdentifierLength} characters of alphanumerics and hyphens (with alphanumeric anchors).",
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

            // Search only authorized tenants. New archive records are message-primary; the
            // bounded correlation index provides compatibility without a state-store scan.
            var matches = new List<(string Tenant, string MessageId, ArchivedCommand Command)>();
            var legacyMatches = new List<(string Tenant, ArchivedCommand Command)>();

            foreach (string tenant in tenantClaims) {
                _ = (activity?.SetTag(EventStoreActivitySource.TagTenantId, tenant));

                ArchivedCommand? directCommand = await archiveStore
                    .ReadCommandAsync(tenant, correlationId, cancellationToken)
                    .ConfigureAwait(false);

                if (directCommand is not null
                    && string.Equals(directCommand.MessageId, correlationId, StringComparison.Ordinal)) {
                    matches.Add((tenant, correlationId, directCommand));
                    continue;
                }

                if (correlationIndex is not null) {
                    CommandCorrelationResolution resolution = await correlationIndex
                        .ResolveAsync(tenant, correlationId, cancellationToken)
                        .ConfigureAwait(false);
                    if (resolution.Outcome == CommandCorrelationResolutionOutcome.Ambiguous) {
                        return CreateCorrelationAmbiguityProblemDetails(requestCorrelationId);
                    }

                    if (resolution is { Outcome: CommandCorrelationResolutionOutcome.Resolved, MessageId: not null }) {
                        ArchivedCommand? indexedCommand = await archiveStore
                            .ReadCommandAsync(tenant, resolution.MessageId, cancellationToken)
                            .ConfigureAwait(false);
                        if (indexedCommand is not null) {
                            matches.Add((tenant, resolution.MessageId, indexedCommand));
                            continue;
                        }
                    }
                }

                if (directCommand is not null) {
                    legacyMatches.Add((tenant, directCommand));
                }
            }

            if (matches.Count > 1 || (matches.Count == 0 && legacyMatches.Count > 1)) {
                return CreateCorrelationAmbiguityProblemDetails(requestCorrelationId);
            }

            ArchivedCommand? archivedCommand = null;
            string? foundTenant = null;
            string? originalMessageId = null;
            if (matches.Count == 1) {
                (foundTenant, originalMessageId, archivedCommand) = matches[0];
            }
            else if (legacyMatches.Count == 1) {
                (foundTenant, archivedCommand) = legacyMatches[0];
                originalMessageId = correlationId;
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
                .ReadStatusAsync(foundTenant, originalMessageId!, cancellationToken)
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

            // Generate a new correlation ID for replay tracking (R2-A7: ULID, not GUID)
            string replayCorrelationId = UniqueIdHelper.GenerateSortableUniqueStringId();

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
                "Replay initiated: CorrelationId={CorrelationId}, OriginalCorrelationId={OriginalCorrelationId}, TenantId={TenantId}, PreviousStatus={PreviousStatus}, IsReplay={IsReplay}",
                replayCorrelationId,
                correlationId,
                foundTenant,
                previousStatus,
                true);

            _ = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

            // AC #1: Return 202 Accepted with replay response
            string absoluteLocationUri = $"{Request.Scheme}://{Request.Host}/api/v1/commands/status/{command.MessageId}";
            Response.Headers["Location"] = absoluteLocationUri;
            Response.Headers["Retry-After"] = "1";

            _ = (activity?.SetStatus(ActivityStatusCode.Ok));
            return Accepted(
                absoluteLocationUri,
                new ReplayCommandResponse(
                    replayCorrelationId,
                    IsReplay: true,
                    PreviousStatus: previousStatus,
                    OriginalCorrelationId: archivedCommand.CorrelationId ?? correlationId,
                    MessageId: command.MessageId,
                    OriginalMessageId: archivedCommand.MessageId));
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            ProtectedDataDiagnosticRedactor.RecordActivityException(activity, ex, "replay");
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
                ["tenantId"] = HttpContext?.Items["RequestTenantId"]?.ToString(),
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
                ["tenantId"] = HttpContext?.Items["RequestTenantId"]?.ToString(),
                ["currentStatus"] = currentStatus,
            },
        };

        if (HttpContext is not null) {
            Response.ContentType = "application/problem+json";
        }

        return new ObjectResult(problemDetails) { StatusCode = StatusCodes.Status409Conflict };
    }

    private ObjectResult CreateCorrelationAmbiguityProblemDetails(string requestCorrelationId)
        => CreateProblemDetails(
            StatusCodes.Status409Conflict,
            ProblemTypeUris.CommandCorrelationAmbiguous,
            "Ambiguous Command Correlation",
            "The correlation identifier maps to multiple commands. Replay using the command MessageId.",
            requestCorrelationId);
}
