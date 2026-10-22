using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Testing.Integration.Benchmarking;

/// <summary>
/// Supplies deterministic, domain-neutral metadata and serialized payload for one benchmark event.
/// </summary>
/// <param name="MessageId">The deterministic event message identifier.</param>
/// <param name="Timestamp">The deterministic event timestamp.</param>
/// <param name="CorrelationId">The deterministic correlation identifier.</param>
/// <param name="CausationId">The deterministic causation identifier.</param>
/// <param name="UserId">The initiating user identifier.</param>
/// <param name="DomainServiceVersion">The domain-service version that produced the event.</param>
/// <param name="EventTypeName">The domain event type name.</param>
/// <param name="MetadataVersion">The EventStore envelope metadata version.</param>
/// <param name="SerializationFormat">The serialized payload format.</param>
/// <param name="Payload">The already serialized event payload.</param>
/// <param name="ProtectionMetadata">The protection metadata matching <paramref name="Payload"/>.</param>
/// <param name="Extensions">Optional non-protection extension metadata.</param>
public sealed record BenchmarkEventDefinition(
    string MessageId,
    DateTimeOffset Timestamp,
    string CorrelationId,
    string CausationId,
    string UserId,
    string DomainServiceVersion,
    string EventTypeName,
    int MetadataVersion,
    string SerializationFormat,
    byte[] Payload,
    EventStorePayloadProtectionMetadata ProtectionMetadata,
    IReadOnlyDictionary<string, string>? Extensions = null);
