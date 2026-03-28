namespace Hexalith.EventStore.Models;

/// <summary>
/// API response model for command replay operations.
/// </summary>
/// <param name="CorrelationId">The new correlation ID for the replayed command.</param>
/// <param name="IsReplay">Indicates this is a replay of a previously failed command.</param>
/// <param name="PreviousStatus">The terminal status that made the original command replayable.</param>
/// <param name="OriginalCorrelationId">The original correlation ID from the failed command, enabling correlation chain reconstruction.</param>
public record ReplayCommandResponse(string CorrelationId, bool IsReplay, string? PreviousStatus, string? OriginalCorrelationId);
