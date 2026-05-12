namespace Hexalith.EventStore.Contracts.Commands;

/// <summary>
/// API-facing response DTO returned when a command is accepted by the EventStore gateway.
/// </summary>
/// <param name="CorrelationId">The correlation identifier used to track command processing status.</param>
public record SubmitCommandResponse(string CorrelationId);
