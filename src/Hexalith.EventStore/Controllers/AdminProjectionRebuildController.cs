using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Server.Projections;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.Controllers;

/// <summary>
/// EventStore-side operator projection rebuild lifecycle endpoints.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/admin/projections")]
[Tags("Admin - Projection Rebuild")]
public sealed partial class AdminProjectionRebuildController(
    IProjectionRebuildCheckpointStore checkpointStore,
    ILogger<AdminProjectionRebuildController> logger) : ControllerBase {
    /// <summary>
    /// Gets the current rebuild status for a projection.
    /// </summary>
    [HttpGet("{tenantId}/{projectionName}/rebuild-status")]
    [ProducesResponseType(typeof(ProjectionRebuildOperation), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRebuildStatus(
        string tenantId,
        string projectionName,
        CancellationToken ct = default) {
        ProjectionRebuildCheckpointScope scope = CreateScope(tenantId, projectionName);
        ProjectionRebuildCheckpoint? checkpoint = await checkpointStore.ReadAsync(scope, ct).ConfigureAwait(false);
        return Ok(ToOperation(scope, checkpoint));
    }

    /// <summary>
    /// Pauses an operator-triggered rebuild.
    /// </summary>
    [HttpPost("{tenantId}/{projectionName}/pause")]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> PauseProjection(
        string tenantId,
        string projectionName,
        CancellationToken ct = default)
        => await SaveLifecycleAsync(
            tenantId,
            projectionName,
            lastAppliedSequence: 0,
            ProjectionRebuildStatus.Paused,
            StreamReplayReasonCodes.RebuildPaused,
            accepted: false,
            ct).ConfigureAwait(false);

    /// <summary>
    /// Resumes a paused operator-triggered rebuild.
    /// </summary>
    [HttpPost("{tenantId}/{projectionName}/resume")]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResumeProjection(
        string tenantId,
        string projectionName,
        CancellationToken ct = default)
        => await SaveLifecycleAsync(
            tenantId,
            projectionName,
            lastAppliedSequence: 0,
            ProjectionRebuildStatus.Running,
            failureReasonCode: null,
            accepted: false,
            ct).ConfigureAwait(false);

    /// <summary>
    /// Resets a projection rebuild checkpoint.
    /// </summary>
    [HttpPost("{tenantId}/{projectionName}/reset")]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ResetProjection(
        string tenantId,
        string projectionName,
        [FromBody] ProjectionResetRequest? request,
        CancellationToken ct = default)
        => await SaveLifecycleAsync(
            tenantId,
            projectionName,
            request?.FromPosition ?? 0,
            ProjectionRebuildStatus.NotStarted,
            failureReasonCode: null,
            accepted: true,
            ct).ConfigureAwait(false);

    /// <summary>
    /// Starts a projection replay/rebuild operation.
    /// </summary>
    [HttpPost("{tenantId}/{projectionName}/replay")]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ReplayProjection(
        string tenantId,
        string projectionName,
        [FromBody] ProjectionReplayRequest request,
        CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(request);
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
            request.FromPosition,
            ProjectionRebuildStatus.Running,
            failureReasonCode: null,
            accepted: true,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Cancels a projection rebuild operation.
    /// </summary>
    [HttpPost("{tenantId}/{projectionName}/cancel")]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelProjection(
        string tenantId,
        string projectionName,
        CancellationToken ct = default)
        => await SaveLifecycleAsync(
            tenantId,
            projectionName,
            lastAppliedSequence: 0,
            ProjectionRebuildStatus.Canceled,
            StreamReplayReasonCodes.RebuildCanceled,
            accepted: false,
            ct).ConfigureAwait(false);

    /// <summary>
    /// Retries a failed or canceled projection rebuild operation.
    /// </summary>
    [HttpPost("{tenantId}/{projectionName}/retry")]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> RetryProjection(
        string tenantId,
        string projectionName,
        CancellationToken ct = default)
        => await SaveLifecycleAsync(
            tenantId,
            projectionName,
            lastAppliedSequence: 0,
            ProjectionRebuildStatus.Retrying,
            failureReasonCode: null,
            accepted: true,
            ct).ConfigureAwait(false);

    private async Task<IActionResult> SaveLifecycleAsync(
        string tenantId,
        string projectionName,
        long lastAppliedSequence,
        ProjectionRebuildStatus status,
        string? failureReasonCode,
        bool accepted,
        CancellationToken ct) {
        ProjectionRebuildCheckpointScope scope = CreateScope(tenantId, projectionName);
        ProjectionRebuildCheckpointSaveResult save = await checkpointStore
            .SaveAsync(scope, lastAppliedSequence, status, failureReasonCode, ct)
            .ConfigureAwait(false);
        if (!save.Succeeded) {
            return MapSaveFailure(save.ReasonCode);
        }

        var result = new AdminOperationResult(
            true,
            scope.OperationId!,
            $"Projection rebuild status is {status}.",
            null);
        return accepted ? Accepted(result) : Ok(result);
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
                StatusCodes.Status409Conflict => ProblemTypeUris.ConcurrencyConflict,
                StatusCodes.Status503ServiceUnavailable => ProblemTypeUris.ServiceUnavailable,
                _ => ProblemTypeUris.InternalServerError,
            },
        };
        problem.Extensions["reasonCode"] = reasonCode;
        return new ObjectResult(problem) { StatusCode = statusCode };
    }

    private static ProjectionRebuildCheckpointScope CreateScope(string tenantId, string projectionName)
        => new(
            tenantId,
            projectionName,
            projectionName,
            AggregateId: null,
            OperationId: $"{projectionName}-rebuild");

    private static ProjectionRebuildOperation ToOperation(
        ProjectionRebuildCheckpointScope scope,
        ProjectionRebuildCheckpoint? checkpoint)
        => new(
            scope.OperationId!,
            scope.Tenant,
            scope.Domain,
            scope.ProjectionName,
            scope.AggregateId,
            checkpoint?.Status ?? ProjectionRebuildStatus.NotStarted,
            checkpoint,
            checkpoint?.UpdatedAt ?? DateTimeOffset.UnixEpoch,
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

/// <summary>
/// EventStore-side projection reset request.
/// </summary>
/// <param name="FromPosition">Optional position to reset from.</param>
public sealed record ProjectionResetRequest(long? FromPosition);

/// <summary>
/// EventStore-side projection replay request.
/// </summary>
/// <param name="FromPosition">The starting stream position.</param>
/// <param name="ToPosition">The ending stream position.</param>
public sealed record ProjectionReplayRequest(long FromPosition, long ToPosition);
