namespace Hexalith.EventStore.CommandApi.Models;

/// <summary>
/// API response model for command replay operations.
/// </summary>
public record ReplayCommandResponse(string CorrelationId, bool IsReplay, string? PreviousStatus);
