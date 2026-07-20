using System.Text.Json;

namespace Hexalith.EventStore.Contracts.Commands;

/// <summary>
/// API-facing request DTO for submitting commands through the EventStore gateway.
/// </summary>
/// <param name="MessageId">The unique command identity and idempotency key.</param>
/// <param name="Tenant">The tenant identifier.</param>
/// <param name="Domain">The target domain name.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="CommandType">The command type discriminator.</param>
/// <param name="Payload">The serialized command payload.</param>
/// <param name="CorrelationId">Optional correlation identifier. Defaults to <paramref name="MessageId"/> when omitted by the gateway.</param>
/// <param name="Extensions">Optional extension metadata.</param>
/// <param name="Idempotency">Optional trusted canonical idempotency descriptor supplied by a registered domain adapter.</param>
public record SubmitCommandRequest(
    string MessageId,
    string Tenant,
    string Domain,
    string AggregateId,
    string CommandType,
    JsonElement Payload,
    string? CorrelationId = null,
    Dictionary<string, string>? Extensions = null,
    CanonicalIdempotencyDescriptor? Idempotency = null);
