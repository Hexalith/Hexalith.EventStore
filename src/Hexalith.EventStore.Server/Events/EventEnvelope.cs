
using System.Runtime.Serialization;

using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Events;
/// <summary>
/// Storage representation of a persisted event in the actor state store.
/// Contains 15 metadata fields (FR11) plus the serialized payload.
/// </summary>
/// <param name="MessageId">The unique event message identifier (ULID).</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="AggregateType">The aggregate type name.</param>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="SequenceNumber">The event sequence number (1-based).</param>
/// <param name="GlobalPosition">The cross-aggregate monotonic position (>= 0).</param>
/// <param name="Timestamp">When the event was persisted.</param>
/// <param name="CorrelationId">The correlation identifier for tracing.</param>
/// <param name="CausationId">The causation identifier linking to the originating command.</param>
/// <param name="UserId">The user who triggered the command.</param>
/// <param name="DomainServiceVersion">The version of the domain service that produced the event.</param>
/// <param name="EventTypeName">The fully qualified event type name for deserialization.</param>
/// <param name="MetadataVersion">The metadata envelope schema version (>= 1 per FR65).</param>
/// <param name="SerializationFormat">The serialization format (e.g., "json").</param>
/// <param name="Payload">The serialized event data.</param>
/// <param name="Extensions">Optional extension metadata.</param>
[DataContract]
public record EventEnvelope(
    [property: DataMember] string MessageId,
    [property: DataMember] string AggregateId,
    [property: DataMember] string AggregateType,
    [property: DataMember] string TenantId,
    [property: DataMember] string Domain,
    [property: DataMember] long SequenceNumber,
    [property: DataMember] long GlobalPosition,
    [property: DataMember] DateTimeOffset Timestamp,
    [property: DataMember] string CorrelationId,
    [property: DataMember] string CausationId,
    [property: DataMember] string UserId,
    [property: DataMember] string DomainServiceVersion,
    [property: DataMember] string EventTypeName,
    [property: DataMember] int MetadataVersion,
    [property: DataMember] string SerializationFormat,
    [property: DataMember] byte[] Payload,
    [property: DataMember] IDictionary<string, string>? Extensions) {
    /// <summary>Gets the aggregate identity derived from this event's tenant, domain, and aggregate ID.</summary>
    public AggregateIdentity Identity => new(TenantId, Domain, AggregateId);

    /// <summary>
    /// Returns a string representation with Payload redacted (SEC-5, Rule #5).
    /// Framework-level enforcement: even if a developer logs the entire EventEnvelope,
    /// the payload is never exposed.
    /// </summary>
    public override string ToString() {
        string extensionKeys = Extensions is not null
            ? string.Join(", ", Extensions.Keys)
            : "none";
        return $"EventEnvelope {{ MessageId = {MessageId}, AggregateId = {AggregateId}, AggregateType = {AggregateType}, TenantId = {TenantId}, Domain = {Domain}, SequenceNumber = {SequenceNumber}, GlobalPosition = {GlobalPosition}, Timestamp = {Timestamp}, CorrelationId = {CorrelationId}, CausationId = {CausationId}, UserId = {UserId}, DomainServiceVersion = {DomainServiceVersion}, EventTypeName = {EventTypeName}, MetadataVersion = {MetadataVersion}, SerializationFormat = {SerializationFormat}, Payload = [REDACTED], Extensions = [{extensionKeys}] }}";
    }
}
