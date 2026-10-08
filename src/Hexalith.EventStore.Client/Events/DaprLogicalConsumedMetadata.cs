namespace Hexalith.EventStore.Client.Events;

/// <summary>Carries unverified metadata consumed from an actor's logical value, preserving logical nulls and extensions.</summary>
/// <remarks>Typed Dapr readback supplies no original JSON property absence versus explicit-null evidence.</remarks>
/// <param name="MessageId">The carried message id.</param>
/// <param name="AggregateId">The carried aggregate id.</param>
/// <param name="AggregateType">The carried aggregate type.</param>
/// <param name="TenantId">The carried tenant id.</param>
/// <param name="Domain">The carried domain.</param>
/// <param name="SequenceNumber">The carried sequence number.</param>
/// <param name="GlobalPosition">The carried global position.</param>
/// <param name="Timestamp">The carried timestamp.</param>
/// <param name="CorrelationId">The carried correlation id.</param>
/// <param name="CausationId">The carried causation id.</param>
/// <param name="UserId">The carried user id.</param>
/// <param name="DomainServiceVersion">The carried domain service version.</param>
/// <param name="EventTypeName">The carried event type name.</param>
/// <param name="MetadataVersion">The carried metadata version.</param>
/// <param name="SerializationFormat">The carried serialization format.</param>
/// <param name="EventContractType">The carried event contract type.</param>
/// <param name="PayloadVersion">The carried payload version.</param>
/// <param name="StoredApplicationDigest">The carried stored application digest.</param>
/// <param name="Extensions">The carried extensions.</param>
/// <param name="ApplicationFormat">The carried application format.</param>
internal sealed record DaprLogicalConsumedMetadata(
    string MessageId, string AggregateId, string AggregateType, string TenantId, string Domain,
    long SequenceNumber, long GlobalPosition, DateTimeOffset Timestamp, string CorrelationId,
    string? CausationId, string? UserId, string? DomainServiceVersion, string EventTypeName,
    int MetadataVersion, string SerializationFormat, string? EventContractType, int? PayloadVersion,
    string? StoredApplicationDigest, IReadOnlyDictionary<string, string>? Extensions, string ApplicationFormat);
