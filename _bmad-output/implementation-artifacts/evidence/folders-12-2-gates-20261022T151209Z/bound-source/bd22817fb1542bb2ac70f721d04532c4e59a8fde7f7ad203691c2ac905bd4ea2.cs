namespace Hexalith.EventStore.Admin.Server.Models;

/// <summary>
/// Request body for projection reset operations.
/// </summary>
/// <param name="FromPosition">Optional position to reset from; null resets from the beginning.</param>
public record ProjectionResetRequest(long? FromPosition);
