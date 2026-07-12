using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Commands;

/// <summary>
/// API-facing response DTO returned when a command is accepted by the EventStore gateway.
/// </summary>
/// <param name="CorrelationId">The correlation identifier used for distributed tracing.</param>
/// <param name="ResultPayload">Optional enriched result payload for completed synchronous command submissions.</param>
/// <param name="MessageId">The canonical message identifier used to track command processing status.</param>
public record SubmitCommandResponse(
    string CorrelationId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonElement? ResultPayload = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MessageId = null);
