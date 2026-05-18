using System.Text.Json;

namespace Hexalith.EventStore.Contracts.Commands;

/// <summary>
/// API-facing response DTO returned when a command is accepted by the EventStore gateway.
/// </summary>
/// <param name="CorrelationId">The correlation identifier used to track command processing status.</param>
/// <param name="ResultPayload">Optional enriched result payload for completed synchronous command submissions.</param>
public record SubmitCommandResponse(string CorrelationId, JsonElement? ResultPayload = null);
