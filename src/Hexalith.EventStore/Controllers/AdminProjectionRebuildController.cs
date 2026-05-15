using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Server.Projections;
using Hexalith.Commons.UniqueIds;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.Controllers;

/// <summary>
/// EventStore-side operator projection rebuild lifecycle endpoints.
/// Requires the authenticated principal to hold the GlobalAdministrator role (P-D1).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/admin/projections")]
[Tags("Admin - Projection Rebuild")]
public sealed partial class AdminProjectionRebuildController(
    IProjectionRebuildCheckpointStore checkpointStore,
    ILogger<AdminProjectionRebuildController> logger,
    IProjectionRebuildOrchestrator? rebuildOrchestrator = null) : ControllerBase {
    /// <summary>
    /// Gets the current rebuild status for a projection.
    /// </summary>
    [HttpGet("{tenantId}/{projectionName}/rebuild-status")]
    [ProducesResponseType(typeof(ProjectionRebuildOperation), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> GetRebuildStatus(
        string tenantId,
        string projectionName,
        CancellationToken ct = default) {
        IActionResult? authFailure = EnsureGlobalAdministrator();
        if (authFailure is not null) {
            return authFailure;
        }

        ProjectionRebuildCheckpointScope scope = CreateScope(tenantId, projectionName);
        ProjectionRebuildCheckpoint? checkpoint = await checkpointStore.ReadAsync(scope, ct).ConfigureAwait(false);
        return Ok(ToOperation(scope, checkpoint));
    }

    /// <summary>
    /// Pauses an operator-triggered rebuild. Returns 404 if no rebuild is active (P5).
    /// </summary>
    [HttpPost("{tenantId}/{projectionName}/pause")]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> PauseProjection(
        string tenantId,
        string projectionName,
        CancellationToken ct = default)
        => await TransitionExistingAsync(
            tenantId,
            projectionName,
            ProjectionRebuildStatus.Paused,
            // P26: pause is not a failure; do not stuff a sentinel into FailureReasonCode.
            failureReasonCode: null,
            accepted: false,
            ct).ConfigureAwait(false);

    /// <summary>
    /// Resumes a paused operator-triggered rebuild. Returns 404 if no rebuild is active (P5).
    /// </summary>
    [HttpPost("{tenantId}/{projectionName}/resume")]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> ResumeProjection(
        string tenantId,
        string projectionName,
        CancellationToken ct = default)
        => await TransitionExistingAsync(
            tenantId,
            projectionName,
            ProjectionRebuildStatus.Running,
            failureReasonCode: null,
            accepted: false,
            ct).ConfigureAwait(false);

    /// <summary>
    /// Resets a projection rebuild checkpoint. NOTE: monotonic clamp on the store currently prevents
    /// LastAppliedSequence regression; this endpoint flips the status only. True rewind is deferred (P3).
    /// </summary>
    [HttpPost("{tenantId}/{projectionName}/reset")]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> ResetProjection(
        string tenantId,
        string projectionName,
        [FromBody] ProjectionResetRequest? request,
        CancellationToken ct = default) {
        IActionResult? authFailure = EnsureGlobalAdministrator();
        if (authFailure is not null) {
            return authFailure;
        }

        if (request is null) {
            return ProblemWithReason(
                StatusCodes.Status400BadRequest,
                "Bad Request",
                "Reset request body is required.",
                StreamReplayReasonCodes.MissingRequiredField);
        }

        long fromPosition = request.FromPosition ?? 0;
        if (fromPosition < 0) {
            return ProblemWithReason(
                StatusCodes.Status400BadRequest,
                "Bad Request",
                "'fromPosition' must be >= 0.",
                StreamReplayReasonCodes.InvalidRange);
        }

        return await SaveLifecycleAsync(
            tenantId,
            projectionName,
            fromPosition,
            ProjectionRebuildStatus.NotStarted,
            failureReasonCode: null,
            accepted: true,
            allowRewind: true,
            toPosition: null,
            runRebuild: false,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts a projection replay/rebuild operation.
    /// </summary>
    [HttpPost("{tenantId}/{projectionName}/replay")]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> ReplayProjection(
        string tenantId,
        string projectionName,
        [FromBody] ProjectionReplayRequest? request,
        CancellationToken ct = default) {
        IActionResult? authFailure = EnsureGlobalAdministrator();
        if (authFailure is not null) {
            return authFailure;
        }

        if (request is null) {
            return ProblemWithReason(
                StatusCodes.Status400BadRequest,
                "Bad Request",
                "Replay request body is required.",
                StreamReplayReasonCodes.MissingRequiredField);
        }

        if (request.FromPosition < 0 || request.ToPosition < request.FromPosition) {
            return ProblemWithReason(
                StatusCodes.Status400BadRequest,
                "Bad Request",
                "Replay range is invalid.",
                StreamReplayReasonCodes.InvalidRange);
        }

        return await SaveLifecycleAsync(
            tenantId,
            projectionName,
            Math.Max(0, request.FromPosition - 1),
            ProjectionRebuildStatus.Running,
            failureReasonCode: null,
            accepted: true,
            allowRewind: true,
            toPosition: request.ToPosition,
            runRebuild: true,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Cancels a projection rebuild operation. Returns 404 if no rebuild is active (P5).
    /// </summary>
    [HttpPost("{tenantId}/{projectionName}/cancel")]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> CancelProjection(
        string tenantId,
        string projectionName,
        CancellationToken ct = default)
        => await TransitionExistingAsync(
            tenantId,
            projectionName,
            ProjectionRebuildStatus.Canceled,
            // P26: cancel is not a failure; status field carries the lifecycle state.
            failureReasonCode: null,
            accepted: false,
            ct).ConfigureAwait(false);

    /// <summary>
    /// Retries a failed or canceled projection rebuild operation. Returns 404 if no rebuild is active (P5).
    /// </summary>
    [HttpPost("{tenantId}/{projectionName}/retry")]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> RetryProjection(
        string tenantId,
        string projectionName,
        CancellationToken ct = default)
        => await TransitionExistingAsync(
            tenantId,
            projectionName,
            ProjectionRebuildStatus.Retrying,
            failureReasonCode: null,
            accepted: true,
            ct).ConfigureAwait(false);

    /// <summary>
    /// Transitions an existing rebuild operation to a new status. Returns 404 when no rebuild exists (P5).
    /// </summary>
    private async Task<IActionResult> TransitionExistingAsync(
        string tenantId,
        string projectionName,
        ProjectionRebuildStatus status,
        string? failureReasonCode,
        bool accepted,
        CancellationToken ct) {
        IActionResult? authFailure = EnsureGlobalAdministrator();
        if (authFailure is not null) {
            return authFailure;
        }

        try {
            ProjectionRebuildCheckpointScope scope = CreateScope(tenantId, projectionName);
            ProjectionRebuildCheckpoint? existing = await checkpointStore.ReadAsync(scope, ct).ConfigureAwait(false);
            if (existing is null) {
                return ProblemWithReason(
                    StatusCodes.Status404NotFound,
                    "Not Found",
                    "No projection rebuild operation found for this projection.",
                    StreamReplayReasonCodes.RebuildOperationNotFound);
            }

            ProjectionRebuildCheckpointSaveResult save = await checkpointStore
                .SaveAsync(scope, existing.LastAppliedSequence, status, failureReasonCode, ct)
                .ConfigureAwait(false);
            if (!save.Succeeded) {
                return MapSaveFailure(save.ReasonCode);
            }

            var result = new AdminOperationResult(
                true,
                existing.OperationId ?? scope.OperationId ?? string.Empty,
                $"Projection rebuild status is {status}.",
                null);
            return accepted ? Accepted(result) : Ok(result);
        }
        catch (ArgumentException ex) {
            // P20: surface key-shape errors as 400 instead of letting them become 500.
            return ProblemWithReason(
                StatusCodes.Status400BadRequest,
                "Bad Request",
                ex.Message,
                StreamReplayReasonCodes.InvalidRange);
        }
    }

    /// <summary>
    /// Writes the lifecycle checkpoint for Replay/Reset. Used when an existing rebuild may or may not exist.
    /// </summary>
    private async Task<IActionResult> SaveLifecycleAsync(
        string tenantId,
        string projectionName,
        long lastAppliedSequence,
        ProjectionRebuildStatus status,
        string? failureReasonCode,
        bool accepted,
        bool allowRewind,
        long? toPosition,
        bool runRebuild,
        CancellationToken ct) {
        try {
            ProjectionRebuildCheckpointScope scope = CreateScope(tenantId, projectionName, UniqueIdHelper.GenerateSortableUniqueStringId());
            ProjectionRebuildCheckpointSaveResult save = allowRewind
                ? await checkpointStore
                    .ResetAsync(scope, lastAppliedSequence, status, failureReasonCode, ct, toPosition)
                    .ConfigureAwait(false)
                : await checkpointStore
                    .SaveAsync(scope, lastAppliedSequence, status, failureReasonCode, ct, toPosition)
                    .ConfigureAwait(false);
            if (!save.Succeeded) {
                return MapSaveFailure(save.ReasonCode);
            }

            var result = new AdminOperationResult(
                true,
                scope.OperationId ?? string.Empty,
                $"Projection rebuild status is {status}.",
                null);
            if (runRebuild && rebuildOrchestrator is not null) {
                await rebuildOrchestrator.RebuildProjectionAsync(scope, ct).ConfigureAwait(false);
            }

            return accepted ? Accepted(result) : Ok(result);
        }
        catch (ArgumentException ex) {
            return ProblemWithReason(
                StatusCodes.Status400BadRequest,
                "Bad Request",
                ex.Message,
                StreamReplayReasonCodes.InvalidRange);
        }
    }

    private IActionResult MapSaveFailure(string? reasonCode)
        => reasonCode switch {
            StreamReplayReasonCodes.CheckpointConflict => ProblemWithReason(
                StatusCodes.Status409Conflict,
                "Conflict",
                "Projection rebuild checkpoint update conflicted with another worker.",
                StreamReplayReasonCodes.CheckpointConflict),
            StreamReplayReasonCodes.CheckpointUnavailable => ProblemWithReason(
                StatusCodes.Status503ServiceUnavailable,
                "Service Unavailable",
                "Projection rebuild checkpoint storage is unavailable.",
                StreamReplayReasonCodes.CheckpointUnavailable),
            _ => ProblemWithReason(
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "Projection rebuild checkpoint update failed.",
                reasonCode ?? StreamReplayReasonCodes.ServiceUnavailable),
        };

    private ObjectResult ProblemWithReason(int statusCode, string title, string detail, string reasonCode) {
        Log.LifecycleRejected(logger, reasonCode);
        var problem = new ProblemDetails {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = statusCode switch {
                StatusCodes.Status400BadRequest => ProblemTypeUris.BadRequest,
                StatusCodes.Status403Forbidden => ProblemTypeUris.Forbidden,
                StatusCodes.Status404NotFound => ProblemTypeUris.NotFound,
                StatusCodes.Status409Conflict => ProblemTypeUris.ConcurrencyConflict,
                StatusCodes.Status503ServiceUnavailable => ProblemTypeUris.ServiceUnavailable,
                _ => ProblemTypeUris.InternalServerError,
            },
        };
        problem.Extensions["reasonCode"] = reasonCode;
        return new ObjectResult(problem) { StatusCode = statusCode };
    }

    /// <summary>
    /// Enforces that the caller holds the GlobalAdministrator role. Returns 403 ProblemDetails when not (P-D1).
    /// Closes the cross-tenant operator hijack: any non-admin authenticated user is denied before scope construction.
    /// </summary>
    private IActionResult? EnsureGlobalAdministrator() {
        if (GlobalAdministratorHelper.IsGlobalAdministrator(User)) {
            return null;
        }

        return ProblemWithReason(
            StatusCodes.Status403Forbidden,
            "Forbidden",
            "Projection rebuild lifecycle endpoints require the GlobalAdministrator role.",
            StreamReplayReasonCodes.UnauthorizedTenant);
    }

    private static ProjectionRebuildCheckpointScope CreateScope(string tenantId, string projectionName, string? operationId = null)
        => new(
            tenantId,
            projectionName,
            projectionName,
            AggregateId: null,
            OperationId: operationId);

    private static ProjectionRebuildOperation ToOperation(
        ProjectionRebuildCheckpointScope scope,
        ProjectionRebuildCheckpoint? checkpoint)
        // P23: when no checkpoint exists, surface UpdatedAt as null rather than UnixEpoch.
        // Caller of ProjectionRebuildOperation must treat StartedAt as a nullable hint.
        => new(
            checkpoint?.OperationId ?? scope.OperationId ?? string.Empty,
            scope.Tenant,
            scope.Domain,
            scope.ProjectionName,
            scope.AggregateId,
            checkpoint?.Status ?? ProjectionRebuildStatus.NotStarted,
            checkpoint,
            checkpoint?.UpdatedAt,
            IsTerminal(checkpoint?.Status) ? checkpoint?.UpdatedAt : null,
            checkpoint?.FailureReasonCode);

    private static bool IsTerminal(ProjectionRebuildStatus? status)
        => status is ProjectionRebuildStatus.Succeeded or ProjectionRebuildStatus.Failed or ProjectionRebuildStatus.Canceled;

    private static partial class Log {
        [LoggerMessage(
            EventId = 1195,
            Level = LogLevel.Warning,
            Message = "Projection rebuild lifecycle request rejected: ReasonCode={ReasonCode}, Stage=ProjectionRebuildLifecycleRejected")]
        public static partial void LifecycleRejected(ILogger logger, string reasonCode);
    }
}
