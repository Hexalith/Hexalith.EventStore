namespace Hexalith.EventStore.Contracts.Tests.Events;

using System.Collections.ObjectModel;

using Hexalith.EventStore.Contracts.Events;

public class EventEnvelopeTests {
    private static EventMetadata CreateMetadata(long sequenceNumber = 1) =>
        new(
            AggregateId: "order-123",
            TenantId: "acme",
            Domain: "payments",
            SequenceNumber: sequenceNumber,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "corr-1",
            CausationId: "cause-1",
            UserId: "user-1",
            DomainServiceVersion: "1.0.0",
            EventTypeName: "OrderCreated",
            SerializationFormat: "json");

    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        var metadata = CreateMetadata();
        byte[] payload = [1, 2, 3];
        var extensions = new ReadOnlyDictionary<string, string>(new Dictionary<string, string> { ["key"] = "value" });

        var envelope = new EventEnvelope(metadata, payload, extensions);

        Assert.Equal(metadata, envelope.Metadata);
        Assert.Same(payload, envelope.Payload);
        Assert.Equal(extensions, envelope.Extensions);
        Assert.NotSame(extensions, envelope.Extensions); // Defensive copy
    }

    [Fact]
    public void Constructor_WithNullExtensions_StoresEmptyReadOnlyDictionary() {
        var metadata = CreateMetadata();
        byte[] payload = [1, 2, 3];

        var envelope = new EventEnvelope(metadata, payload, null);

        Assert.NotNull(envelope.Extensions);
        Assert.Empty(envelope.Extensions);
    }

    [Fact]
    public void Extensions_IsIReadOnlyDictionary() {
        var metadata = CreateMetadata();
        byte[] payload = [1, 2, 3];
        var dict = new Dictionary<string, string> { ["k"] = "v" };

        var envelope = new EventEnvelope(metadata, payload, dict);

        Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(envelope.Extensions);
    }

    [Fact]
    public void RecordEquality_SameMetadataDifferentPayloadArrays_AreNotEqual() {
        var metadata = CreateMetadata();
        byte[] payload1 = [1, 2, 3];
        byte[] payload2 = [1, 2, 3];

        var envelope1 = new EventEnvelope(metadata, payload1, null);
        var envelope2 = new EventEnvelope(metadata, payload2, null);

        // Record equality uses reference equality for byte[] — documented limitation
        Assert.NotEqual(envelope1, envelope2);
    }

    [Fact]
    public void RecordEquality_SamePayloadReference_AreEqual() {
        var metadata = CreateMetadata();
        byte[] payload = [1, 2, 3];

        var envelope1 = new EventEnvelope(metadata, payload, null);
        var envelope2 = new EventEnvelope(metadata, payload, null);

        Assert.Equal(envelope1, envelope2);
    }

    [Fact]
    public void PayloadComparison_UseSequenceEqual_ForByteComparison() {
        var metadata = CreateMetadata();
        byte[] payload1 = [1, 2, 3];
        byte[] payload2 = [1, 2, 3];

        var envelope1 = new EventEnvelope(metadata, payload1, null);
        var envelope2 = new EventEnvelope(metadata, payload2, null);

        Assert.True(envelope1.Payload.SequenceEqual(envelope2.Payload));
    }

    [Fact]
    public void Constructor_WithNullMetadata_ThrowsArgumentNullException() {
        Assert.Throws<ArgumentNullException>(() => new EventEnvelope(null!, [1, 2, 3], null));
    }

    [Fact]
    public void Constructor_WithNullPayload_ThrowsArgumentNullException() {
        var metadata = CreateMetadata();
        Assert.Throws<ArgumentNullException>(() => new EventEnvelope(metadata, null!, null));
    }

    [Fact]
    public void Extensions_DefensiveCopy_PreventsMutation() {
        var dict = new Dictionary<string, string> { ["key"] = "original" };
        var envelope = new EventEnvelope(CreateMetadata(), [1], dict);

        dict["key"] = "mutated";

        Assert.Equal("original", envelope.Extensions["key"]);
    }

    [Fact]
    public void Metadata_ExposesAll11Fields() {
        var metadata = CreateMetadata();
        byte[] payload = [1, 2, 3];
        var envelope = new EventEnvelope(metadata, payload, null);

        Assert.Equal("order-123", envelope.Metadata.AggregateId);
        Assert.Equal("acme", envelope.Metadata.TenantId);
        Assert.Equal("payments", envelope.Metadata.Domain);
        Assert.Equal(1, envelope.Metadata.SequenceNumber);
        Assert.Equal("corr-1", envelope.Metadata.CorrelationId);
        Assert.Equal("cause-1", envelope.Metadata.CausationId);
        Assert.Equal("user-1", envelope.Metadata.UserId);
        Assert.Equal("1.0.0", envelope.Metadata.DomainServiceVersion);
        Assert.Equal("OrderCreated", envelope.Metadata.EventTypeName);
        Assert.Equal("json", envelope.Metadata.SerializationFormat);
    }
}
