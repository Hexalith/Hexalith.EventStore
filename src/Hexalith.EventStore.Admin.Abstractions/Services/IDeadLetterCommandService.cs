using Hexalith.EventStore.Admin.Abstractions.Models.Common;

namespace Hexalith.EventStore.Admin.Abstractions.Services;

/// <summary>
/// Service interface for operator-level dead-letter management operations. CQRS-split — writes only.
/// </summary>
public interface IDeadLetterCommandService {
    /// <summary>
    /// Retries the specified dead-letter messages.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="messageIds">The message IDs to retry.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> RetryDeadLettersAsync(string tenantId, IReadOnlyList<string> messageIds, CancellationToken ct = default);

    /// <summary>Submits or reads a recovery decision identified by a stable caller action ID.</summary>
    /// <param name="tenantId">The authorized tenant scope.</param>
    /// <param name="messageIds">The selected retained message IDs.</param>
    /// <param name="actionId">The stable identity of this decision.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The persisted operation state.</returns>
    Task<AdminOperationResult> RetryDeadLettersAsync(string tenantId, IReadOnlyList<string> messageIds, string actionId, CancellationToken ct = default)
        => Task.FromException<AdminOperationResult>(new NotSupportedException("Idempotent recovery is unavailable."));

    /// <summary>
    /// Skips the specified dead-letter messages, removing them from the queue.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="messageIds">The message IDs to skip.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> SkipDeadLettersAsync(string tenantId, IReadOnlyList<string> messageIds, CancellationToken ct = default);

    /// <summary>Submits or reads a recovery decision identified by a stable caller action ID.</summary>
    /// <param name="tenantId">The authorized tenant scope.</param>
    /// <param name="messageIds">The selected retained message IDs.</param>
    /// <param name="actionId">The stable identity of this decision.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The persisted operation state.</returns>
    Task<AdminOperationResult> SkipDeadLettersAsync(string tenantId, IReadOnlyList<string> messageIds, string actionId, CancellationToken ct = default)
        => Task.FromException<AdminOperationResult>(new NotSupportedException("Idempotent recovery is unavailable."));

    /// <summary>
    /// Archives the specified dead-letter messages.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="messageIds">The message IDs to archive.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> ArchiveDeadLettersAsync(string tenantId, IReadOnlyList<string> messageIds, CancellationToken ct = default);

    /// <summary>Submits or reads a recovery decision identified by a stable caller action ID.</summary>
    /// <param name="tenantId">The authorized tenant scope.</param>
    /// <param name="messageIds">The selected retained message IDs.</param>
    /// <param name="actionId">The stable identity of this decision.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The persisted operation state.</returns>
    Task<AdminOperationResult> ArchiveDeadLettersAsync(string tenantId, IReadOnlyList<string> messageIds, string actionId, CancellationToken ct = default)
        => Task.FromException<AdminOperationResult>(new NotSupportedException("Idempotent recovery is unavailable."));
}
