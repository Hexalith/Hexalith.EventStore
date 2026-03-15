
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Testing.Builders;

namespace Hexalith.EventStore.Testing.Tests.Builders;

public class EventEnvelopeBuilderTests {
    [Fact]
    public void Build_produces_valid_event_envelope_with_defaults() {
        EventEnvelope envelope = new EventEnvelopeBuilder().Build();

        Assert.NotNull(envelope.Metadata);
        Assert.NotNull(envelope.Payload);
        Assert.NotEmpty(envelope.Metadata.MessageId);
        Assert.Equal("test-tenant:test-domain:test-agg-001", envelope.Metadata.AggregateId);
        Assert.Equal("test-aggregate", envelope.Metadata.AggregateType);
        Assert.Equal("test-tenant", envelope.Metadata.TenantId);
        Assert.Equal("test-domain", envelope.Metadata.Domain);
        Assert.Equal(1, envelope.Metadata.SequenceNumber);
        Assert.Equal(0, envelope.Metadata.GlobalPosition);
        Assert.NotEqual(default, envelope.Metadata.Timestamp);
        Assert.NotEmpty(envelope.Metadata.CorrelationId);
        Assert.NotEmpty(envelope.Metadata.CausationId);
        Assert.Equal("test-user", envelope.Metadata.UserId);
        Assert.Equal("1.0.0", envelope.Metadata.DomainServiceVersion);
        Assert.Equal("TestEvent", envelope.Metadata.EventTypeName);
        Assert.Equal(1, envelope.Metadata.MetadataVersion);
        Assert.Equal("json", envelope.Metadata.SerializationFormat);
    }

    [Fact]
    public void Build_fluent_overrides_work() {
        EventEnvelope envelope = new EventEnvelopeBuilder()
            .WithSequenceNumber(5)
            .WithEventTypeName("OrderCreated")
            .WithSerializationFormat("protobuf")
            .Build();

        Assert.Equal(5, envelope.Metadata.SequenceNumber);
        Assert.Equal("OrderCreated", envelope.Metadata.EventTypeName);
        Assert.Equal("protobuf", envelope.Metadata.SerializationFormat);
    }

    [Fact]
    public void Build_composite_aggregate_id_reflects_tenant_and_domain_overrides() {
        EventEnvelope envelope = new EventEnvelopeBuilder()
            .WithTenantId("acme")
            .WithDomain("billing")
            .WithAggregateIdPart("inv-001")
            .Build();

        Assert.Equal("acme:billing:inv-001", envelope.Metadata.AggregateId);
        Assert.Equal("acme", envelope.Metadata.TenantId);
        Assert.Equal("billing", envelope.Metadata.Domain);
    }

    [Fact]
    public void Build_composite_aggregate_id_consistent_with_defaults() {
        EventEnvelope envelope = new EventEnvelopeBuilder().Build();

        Assert.Equal($"{envelope.Metadata.TenantId}:{envelope.Metadata.Domain}:test-agg-001", envelope.Metadata.AggregateId);
    }
}
