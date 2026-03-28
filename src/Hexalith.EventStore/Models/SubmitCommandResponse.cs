namespace Hexalith.EventStore.Models;

/// <summary>
/// API response DTO containing the correlation ID for command tracking.
/// </summary>
public record SubmitCommandResponse(string CorrelationId);
