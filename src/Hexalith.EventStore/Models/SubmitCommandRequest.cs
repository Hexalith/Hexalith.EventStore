
using System.Text.Json;

namespace Hexalith.EventStore.Models;
/// <summary>
/// API-facing request DTO for submitting commands.
/// Separate from <see cref="Contracts.Commands.CommandEnvelope"/> which is the internal domain type.
/// </summary>
/// <param name="MessageId">The unique command identity and idempotency key (ULID string). Required — client owns generation (FR49).</param>
/// <param name="Tenant">The tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="CommandType">The fully qualified command type name.</param>
/// <param name="Payload">The serialized command payload.</param>
/// <param name="CorrelationId">Optional correlation identifier for cross-system tracing (FR4). Defaults to MessageId when not provided.</param>
/// <param name="Extensions">Optional extension metadata.</param>
public record SubmitCommandRequest(
    string MessageId,
    string Tenant,
    string Domain,
    string AggregateId,
    string CommandType,
    JsonElement Payload,
    string? CorrelationId = null,
    Dictionary<string, string>? Extensions = null);
