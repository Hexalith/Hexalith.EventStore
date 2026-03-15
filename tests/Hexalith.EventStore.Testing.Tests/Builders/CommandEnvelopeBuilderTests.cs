
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Testing.Builders;

namespace Hexalith.EventStore.Testing.Tests.Builders;

public class CommandEnvelopeBuilderTests {
    [Fact]
    public void Build_produces_valid_command_envelope_with_defaults() {
        CommandEnvelope envelope = new CommandEnvelopeBuilder().Build();

        Assert.NotEmpty(envelope.MessageId);
        Assert.Equal("test-tenant", envelope.AggregateIdentity.TenantId);
        Assert.Equal("test-domain", envelope.AggregateIdentity.Domain);
        Assert.Equal("test-agg-001", envelope.AggregateIdentity.AggregateId);
        Assert.Equal("TestCommand", envelope.CommandType);
        Assert.NotNull(envelope.Payload);
        Assert.NotEmpty(envelope.CorrelationId);
        Assert.Equal("test-user", envelope.UserId);
    }

    [Fact]
    public void Build_fluent_overrides_work() {
        CommandEnvelope envelope = new CommandEnvelopeBuilder()
            .WithMessageId("msg-override")
            .WithTenantId("acme")
            .WithDomain("billing")
            .WithAggregateId("inv-001")
            .WithCommandType("CreateInvoice")
            .WithUserId("admin")
            .WithCorrelationId("corr-123")
            .WithCausationId("cause-456")
            .Build();

        Assert.Equal("msg-override", envelope.MessageId);
        Assert.Equal("acme", envelope.AggregateIdentity.TenantId);
        Assert.Equal("billing", envelope.AggregateIdentity.Domain);
        Assert.Equal("inv-001", envelope.AggregateIdentity.AggregateId);
        Assert.Equal("CreateInvoice", envelope.CommandType);
        Assert.Equal("admin", envelope.UserId);
        Assert.Equal("corr-123", envelope.CorrelationId);
        Assert.Equal("cause-456", envelope.CausationId);
    }

    [Fact]
    public void WithMessageId_overrides_default_generated_id() {
        CommandEnvelope envelope = new CommandEnvelopeBuilder()
            .WithMessageId("custom-msg-id")
            .Build();

        Assert.Equal("custom-msg-id", envelope.MessageId);
    }

    [Fact]
    public void Default_MessageId_is_unique_per_builder() {
        CommandEnvelope envelope1 = new CommandEnvelopeBuilder().Build();
        CommandEnvelope envelope2 = new CommandEnvelopeBuilder().Build();

        Assert.NotEqual(envelope1.MessageId, envelope2.MessageId);
    }
}
