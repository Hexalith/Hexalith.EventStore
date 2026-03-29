
using System.Collections.ObjectModel;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Contracts.Tests.Events;

public class EventEnvelopeTests {
    private static EventMetadata CreateMetadata(long sequenceNumber = 1) =>
        new(
            MessageId: "msg-001",
            AggregateId: "order-123",
            AggregateType: "order",
            TenantId: "acme",
            Domain: "payments",
            SequenceNumber: sequenceNumber,
            GlobalPosition: 0,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "corr-1",
            CausationId: "cause-1",
            UserId: "user-1",
            DomainServiceVersion: "1.0.0",
            EventTypeName: "OrderCreated",
            MetadataVersion: 1,
            SerializationFormat: "json");

    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        EventMetadata metadata = CreateMetadata();
        byte[] payload = [1, 2, 3];
        var extensions = new ReadOnlyDictionary<string, string>(new Dictionary<string, string> { ["key"] = "value" });

        var envelope = new EventEnvelope(metadata, payload, extensions);

        envelope.Metadata.ShouldBe(metadata);
        envelope.Payload.ShouldBeSameAs(payload);
        envelope.Extensions.ShouldBe(extensions);
        envelope.Extensions.ShouldNotBeSameAs(extensions); // Defensive copy
    }

    [Fact]
    public void Constructor_WithNullExtensions_StoresEmptyReadOnlyDictionary() {
        EventMetadata metadata = CreateMetadata();
        byte[] payload = [1, 2, 3];

        var envelope = new EventEnvelope(metadata, payload, null);

        envelope.Extensions.ShouldNotBeNull();
        envelope.Extensions.ShouldBeEmpty();
    }

    [Fact]
    public void Extensions_IsIReadOnlyDictionary() {
        EventMetadata metadata = CreateMetadata();
        byte[] payload = [1, 2, 3];
        var dict = new Dictionary<string, string> { ["k"] = "v" };

        var envelope = new EventEnvelope(metadata, payload, dict);

        _ = envelope.Extensions.ShouldBeAssignableTo<IReadOnlyDictionary<string, string>>();
    }

    [Fact]
    public void RecordEquality_SameMetadataDifferentPayloadArrays_AreNotEqual() {
        EventMetadata metadata = CreateMetadata();
        byte[] payload1 = [1, 2, 3];
        byte[] payload2 = [1, 2, 3];

        var envelope1 = new EventEnvelope(metadata, payload1, null);
        var envelope2 = new EventEnvelope(metadata, payload2, null);

        // Record equality uses reference equality for byte[] — documented limitation
        envelope2.ShouldNotBe(envelope1);
    }

    [Fact]
    public void RecordEquality_SamePayloadReference_AreEqual() {
        EventMetadata metadata = CreateMetadata();
        byte[] payload = [1, 2, 3];

        var envelope1 = new EventEnvelope(metadata, payload, null);
        var envelope2 = new EventEnvelope(metadata, payload, null);

        envelope2.ShouldBe(envelope1);
    }

    [Fact]
    public void PayloadComparison_UseSequenceEqual_ForByteComparison() {
        EventMetadata metadata = CreateMetadata();
        byte[] payload1 = [1, 2, 3];
        byte[] payload2 = [1, 2, 3];

        var envelope1 = new EventEnvelope(metadata, payload1, null);
        var envelope2 = new EventEnvelope(metadata, payload2, null);

        envelope1.Payload.SequenceEqual(envelope2.Payload).ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithNullMetadata_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() => new EventEnvelope(null!, [1, 2, 3], null));

    [Fact]
    public void Constructor_WithNullPayload_ThrowsArgumentNullException() {
        EventMetadata metadata = CreateMetadata();
        _ = Should.Throw<ArgumentNullException>(() => new EventEnvelope(metadata, null!, null));
    }

    [Fact]
    public void Extensions_DefensiveCopy_PreventsMutation() {
        var dict = new Dictionary<string, string> { ["key"] = "original" };
        var envelope = new EventEnvelope(CreateMetadata(), [1], dict);

        dict["key"] = "mutated";

        envelope.Extensions["key"].ShouldBe("original");
    }

    [Fact]
    public void Metadata_ExposesAll15Fields() {
        EventMetadata metadata = CreateMetadata();
        byte[] payload = [1, 2, 3];
        var envelope = new EventEnvelope(metadata, payload, null);

        envelope.Metadata.MessageId.ShouldBe("msg-001");
        envelope.Metadata.AggregateId.ShouldBe("order-123");
        envelope.Metadata.AggregateType.ShouldBe("order");
        envelope.Metadata.TenantId.ShouldBe("acme");
        envelope.Metadata.Domain.ShouldBe("payments");
        envelope.Metadata.SequenceNumber.ShouldBe(1);
        envelope.Metadata.GlobalPosition.ShouldBe(0);
        envelope.Metadata.Timestamp.ShouldNotBe(default);
        envelope.Metadata.CorrelationId.ShouldBe("corr-1");
        envelope.Metadata.CausationId.ShouldBe("cause-1");
        envelope.Metadata.UserId.ShouldBe("user-1");
        envelope.Metadata.DomainServiceVersion.ShouldBe("1.0.0");
        envelope.Metadata.EventTypeName.ShouldBe("OrderCreated");
        envelope.Metadata.MetadataVersion.ShouldBe(1);
        envelope.Metadata.SerializationFormat.ShouldBe("json");
    }
}
