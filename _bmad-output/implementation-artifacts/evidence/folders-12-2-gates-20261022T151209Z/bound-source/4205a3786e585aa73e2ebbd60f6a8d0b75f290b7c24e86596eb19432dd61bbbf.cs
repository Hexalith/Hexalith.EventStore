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

    /// <summary>
    /// Skips the specified dead-letter messages, removing them from the queue.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="messageIds">The message IDs to skip.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> SkipDeadLettersAsync(string tenantId, IReadOnlyList<string> messageIds, CancellationToken ct = default);

    /// <summary>
    /// Archives the specified dead-letter messages.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="messageIds">The message IDs to archive.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> ArchiveDeadLettersAsync(string tenantId, IReadOnlyList<string> messageIds, CancellationToken ct = default);
}
