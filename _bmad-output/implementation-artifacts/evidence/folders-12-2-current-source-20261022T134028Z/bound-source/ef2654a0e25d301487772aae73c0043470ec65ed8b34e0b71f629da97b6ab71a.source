
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.Serialization;

public class CommandEnvelopeSerializationTests {
    [Fact]
    public void CommandEnvelope_JsonRoundtrip_PreservesAllFields() {
        // Arrange
        var original = new CommandEnvelope(
            MessageId: "msg-1",
            TenantId: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: [1, 2, 3, 4, 5],
            CorrelationId: "corr-123",
            CausationId: "cause-456",
            UserId: "user-789",
            Extensions: new Dictionary<string, string> { ["key"] = "value" });

        // Act
        string json = JsonSerializer.Serialize(original);
        CommandEnvelope? deserialized = JsonSerializer.Deserialize<CommandEnvelope>(json);

        // Assert
        _ = deserialized.ShouldNotBeNull();
        deserialized.TenantId.ShouldBe(original.TenantId);
        deserialized.Domain.ShouldBe(original.Domain);
        deserialized.AggregateId.ShouldBe(original.AggregateId);
        deserialized.CommandType.ShouldBe(original.CommandType);
        deserialized.CorrelationId.ShouldBe(original.CorrelationId);
        deserialized.CausationId.ShouldBe(original.CausationId);
        deserialized.UserId.ShouldBe(original.UserId);
    }

    [Fact]
    public void CommandEnvelope_ByteArrayPayload_SerializesAsBase64() {
        // Arrange
        byte[] payload = [0xFF, 0x00, 0xAB, 0xCD];
        var envelope = new CommandEnvelope(
            MessageId: "msg-2",
            TenantId: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: payload,
            CorrelationId: "corr-123",
            CausationId: null,
            UserId: "system",
            Extensions: null);

        // Act
        string json = JsonSerializer.Serialize(envelope);
        CommandEnvelope? deserialized = JsonSerializer.Deserialize<CommandEnvelope>(json);

        // Assert
        _ = deserialized.ShouldNotBeNull();
        deserialized.Payload.ShouldBe(payload);
    }

    [Fact]
    public void CommandEnvelope_NullExtensions_RoundtripsCorrectly() {
        // Arrange
        var original = new CommandEnvelope(
            MessageId: "msg-3",
            TenantId: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: [1],
            CorrelationId: "corr-123",
            CausationId: null,
            UserId: "system",
            Extensions: null);

        // Act
        string json = JsonSerializer.Serialize(original);
        CommandEnvelope? deserialized = JsonSerializer.Deserialize<CommandEnvelope>(json);

        // Assert
        _ = deserialized.ShouldNotBeNull();
        deserialized.Extensions.ShouldBeNull();
    }
}
