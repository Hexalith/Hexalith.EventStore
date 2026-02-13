namespace Hexalith.EventStore.Testing.Builders;

using System.Text;

using Hexalith.EventStore.Contracts.Events;

/// <summary>
/// Fluent builder for creating <see cref="EventEnvelope"/> instances with sensible defaults for testing.
/// </summary>
public sealed class EventEnvelopeBuilder
{
    private string _tenantId = "test-tenant";
    private string _domain = "test-domain";
    private string _aggregateIdPart = "test-agg-001";
    private long _sequenceNumber = 1;
    private DateTimeOffset _timestamp = DateTimeOffset.UtcNow;
    private string _correlationId = Guid.NewGuid().ToString();
    private string _causationId = Guid.NewGuid().ToString();
    private string _userId = "test-user";
    private string _domainServiceVersion = "1.0.0";
    private string _eventTypeName = "TestEvent";
    private string _serializationFormat = "json";
    private byte[] _payload = Encoding.UTF8.GetBytes("{}");
    private IReadOnlyDictionary<string, string>? _extensions;

    /// <summary>Sets the aggregate identifier part (the ID portion, not the full colon-separated form).</summary>
    /// <param name="aggregateIdPart">The aggregate identifier part.</param>
    /// <returns>This builder instance.</returns>
    public EventEnvelopeBuilder WithAggregateIdPart(string aggregateIdPart) { _aggregateIdPart = aggregateIdPart; return this; }

    /// <summary>Sets the tenant identifier.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>This builder instance.</returns>
    public EventEnvelopeBuilder WithTenantId(string tenantId) { _tenantId = tenantId; return this; }

    /// <summary>Sets the domain name.</summary>
    /// <param name="domain">The domain name.</param>
    /// <returns>This builder instance.</returns>
    public EventEnvelopeBuilder WithDomain(string domain) { _domain = domain; return this; }

    /// <summary>Sets the event sequence number.</summary>
    /// <param name="sequenceNumber">The sequence number (must be >= 1).</param>
    /// <returns>This builder instance.</returns>
    public EventEnvelopeBuilder WithSequenceNumber(long sequenceNumber) { _sequenceNumber = sequenceNumber; return this; }

    /// <summary>Sets the event timestamp.</summary>
    /// <param name="timestamp">The timestamp.</param>
    /// <returns>This builder instance.</returns>
    public EventEnvelopeBuilder WithTimestamp(DateTimeOffset timestamp) { _timestamp = timestamp; return this; }

    /// <summary>Sets the correlation identifier.</summary>
    /// <param name="correlationId">The correlation identifier.</param>
    /// <returns>This builder instance.</returns>
    public EventEnvelopeBuilder WithCorrelationId(string correlationId) { _correlationId = correlationId; return this; }

    /// <summary>Sets the causation identifier.</summary>
    /// <param name="causationId">The causation identifier.</param>
    /// <returns>This builder instance.</returns>
    public EventEnvelopeBuilder WithCausationId(string causationId) { _causationId = causationId; return this; }

    /// <summary>Sets the user identifier.</summary>
    /// <param name="userId">The user identifier.</param>
    /// <returns>This builder instance.</returns>
    public EventEnvelopeBuilder WithUserId(string userId) { _userId = userId; return this; }

    /// <summary>Sets the domain service version.</summary>
    /// <param name="domainServiceVersion">The version string.</param>
    /// <returns>This builder instance.</returns>
    public EventEnvelopeBuilder WithDomainServiceVersion(string domainServiceVersion) { _domainServiceVersion = domainServiceVersion; return this; }

    /// <summary>Sets the event type name.</summary>
    /// <param name="eventTypeName">The event type name.</param>
    /// <returns>This builder instance.</returns>
    public EventEnvelopeBuilder WithEventTypeName(string eventTypeName) { _eventTypeName = eventTypeName; return this; }

    /// <summary>Sets the serialization format.</summary>
    /// <param name="serializationFormat">The serialization format.</param>
    /// <returns>This builder instance.</returns>
    public EventEnvelopeBuilder WithSerializationFormat(string serializationFormat) { _serializationFormat = serializationFormat; return this; }

    /// <summary>Sets the serialized payload.</summary>
    /// <param name="payload">The payload bytes.</param>
    /// <returns>This builder instance.</returns>
    public EventEnvelopeBuilder WithPayload(byte[] payload) { _payload = payload; return this; }

    /// <summary>Sets the extension metadata.</summary>
    /// <param name="extensions">The extension metadata.</param>
    /// <returns>This builder instance.</returns>
    public EventEnvelopeBuilder WithExtensions(IReadOnlyDictionary<string, string>? extensions) { _extensions = extensions; return this; }

    /// <summary>Builds the <see cref="EventEnvelope"/> instance.</summary>
    /// <returns>A new <see cref="EventEnvelope"/> with the configured values.</returns>
    public EventEnvelope Build()
    {
        var metadata = new EventMetadata(
            $"{_tenantId}:{_domain}:{_aggregateIdPart}",
            _tenantId,
            _domain,
            _sequenceNumber,
            _timestamp,
            _correlationId,
            _causationId,
            _userId,
            _domainServiceVersion,
            _eventTypeName,
            _serializationFormat);

        return new EventEnvelope(metadata, _payload, _extensions);
    }
}
