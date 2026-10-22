using Hexalith.EventStore.Admin.Abstractions.Models.Common;

namespace Hexalith.EventStore.Admin.Abstractions.Services;

/// <summary>
/// Service interface for operator-level projection management operations (FR73).
/// </summary>
public interface IProjectionCommandService {
    /// <summary>
    /// Pauses a running projection.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="projectionName">The projection name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> PauseProjectionAsync(string tenantId, string projectionName, CancellationToken ct = default);

    /// <summary>
    /// Resumes a paused projection.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="projectionName">The projection name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> ResumeProjectionAsync(string tenantId, string projectionName, CancellationToken ct = default);

    /// <summary>
    /// Resets a projection, optionally from a specific position.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="projectionName">The projection name.</param>
    /// <param name="fromPosition">Optional position to reset from; null resets from the beginning.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> ResetProjectionAsync(string tenantId, string projectionName, long? fromPosition, CancellationToken ct = default);

    /// <summary>
    /// Replays a projection between two positions.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="projectionName">The projection name.</param>
    /// <param name="fromPosition">The starting position for replay.</param>
    /// <param name="toPosition">The ending position for replay.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> ReplayProjectionAsync(string tenantId, string projectionName, long fromPosition, long toPosition, CancellationToken ct = default);
}
