namespace Hexalith.EventStore.Testing.Assertions;

using Hexalith.EventStore.Contracts.Events;

using Xunit;

/// <summary>
/// Static assertion helpers for verifying <see cref="EventEnvelope"/> metadata completeness.
/// </summary>
public static class EventEnvelopeAssertions
{
    /// <summary>
    /// Verifies all 11 metadata fields are populated and the payload is non-null.
    /// </summary>
    /// <param name="envelope">The event envelope to verify.</param>
    public static void ShouldHaveValidMetadata(EventEnvelope envelope)
    {
        Assert.NotNull(envelope);
        Assert.NotNull(envelope.Metadata);
        Assert.NotNull(envelope.Payload);

        EventMetadata m = envelope.Metadata;
        Assert.False(string.IsNullOrWhiteSpace(m.AggregateId), "AggregateId must not be null or whitespace.");
        Assert.False(string.IsNullOrWhiteSpace(m.TenantId), "TenantId must not be null or whitespace.");
        Assert.False(string.IsNullOrWhiteSpace(m.Domain), "Domain must not be null or whitespace.");
        Assert.True(m.SequenceNumber >= 1, $"SequenceNumber must be >= 1, got {m.SequenceNumber}.");
        Assert.NotEqual(default, m.Timestamp);
        Assert.False(string.IsNullOrWhiteSpace(m.CorrelationId), "CorrelationId must not be null or whitespace.");
        Assert.False(string.IsNullOrWhiteSpace(m.CausationId), "CausationId must not be null or whitespace.");
        Assert.False(string.IsNullOrWhiteSpace(m.UserId), "UserId must not be null or whitespace.");
        Assert.False(string.IsNullOrWhiteSpace(m.DomainServiceVersion), "DomainServiceVersion must not be null or whitespace.");
        Assert.False(string.IsNullOrWhiteSpace(m.EventTypeName), "EventTypeName must not be null or whitespace.");
        Assert.False(string.IsNullOrWhiteSpace(m.SerializationFormat), "SerializationFormat must not be null or whitespace.");
    }
}
