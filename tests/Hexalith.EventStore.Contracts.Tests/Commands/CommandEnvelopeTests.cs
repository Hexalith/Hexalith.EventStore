using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Tests.Commands;

public class CommandEnvelopeTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        byte[] payload = [1, 2, 3];
        var envelope = new CommandEnvelope(
            MessageId: "msg-1",
            TenantId: "acme",
            Domain: "payments",
            AggregateId: "order-123",
            CommandType: "CreateOrder",
            Payload: payload,
            CorrelationId: "corr-1",
            CausationId: null,
            UserId: "user-1",
            Extensions: null);

        envelope.MessageId.ShouldBe("msg-1");
        envelope.TenantId.ShouldBe("acme");
        envelope.Domain.ShouldBe("payments");
        envelope.AggregateId.ShouldBe("order-123");
        envelope.CommandType.ShouldBe("CreateOrder");
        envelope.Payload.ShouldBeSameAs(payload);
        envelope.CorrelationId.ShouldBe("corr-1");
        envelope.CausationId.ShouldBeNull();
        envelope.UserId.ShouldBe("user-1");
        envelope.Extensions.ShouldBeNull();
    }

    [Fact]
    public void AggregateIdentity_DerivesCorrectIdentity() {
        var envelope = new CommandEnvelope(
            MessageId: "msg-2",
            TenantId: "acme",
            Domain: "payments",
            AggregateId: "order-123",
            CommandType: "CreateOrder",
            Payload: [1],
            CorrelationId: "corr-1",
            CausationId: null,
            UserId: "user-1",
            Extensions: null);

        AggregateIdentity identity = envelope.AggregateIdentity;

        identity.TenantId.ShouldBe("acme");
        identity.Domain.ShouldBe("payments");
        identity.AggregateId.ShouldBe("order-123");
    }

    [Fact]
    public void Extensions_IsNullable() {
        var envelope = new CommandEnvelope(
            MessageId: "msg-3",
            TenantId: "acme",
            Domain: "payments",
            AggregateId: "order-123",
            CommandType: "CreateOrder",
            Payload: [1],
            CorrelationId: "c1",
            CausationId: null,
            UserId: "u1",
            Extensions: null);

        envelope.Extensions.ShouldBeNull();
    }

    [Fact]
    public void Extensions_AcceptsDictionary() {
        var extensions = new Dictionary<string, string> { ["key"] = "value" };
        var envelope = new CommandEnvelope(
            MessageId: "msg-4",
            TenantId: "acme",
            Domain: "payments",
            AggregateId: "order-123",
            CommandType: "CreateOrder",
            Payload: [1],
            CorrelationId: "c1",
            CausationId: null,
            UserId: "u1",
            Extensions: extensions);

        _ = envelope.Extensions.ShouldNotBeNull();
        envelope.Extensions["key"].ShouldBe("value");
    }

    [Fact]
    public void CausationId_IsNullable() {
        var envelope = new CommandEnvelope(
            MessageId: "msg-5",
            TenantId: "acme",
            Domain: "payments",
            AggregateId: "order-123",
            CommandType: "CreateOrder",
            Payload: [1],
            CorrelationId: "c1",
            CausationId: "cause-1",
            UserId: "u1",
            Extensions: null);

        envelope.CausationId.ShouldBe("cause-1");
    }

    [Fact]
    public void Constructor_WithNullPayload_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() =>
            new CommandEnvelope(
                MessageId: "msg-6",
                TenantId: "acme",
                Domain: "payments",
                AggregateId: "order-123",
                CommandType: "CreateOrder",
                Payload: null!,
                CorrelationId: "c1",
                CausationId: null,
                UserId: "u1",
                Extensions: null));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WithInvalidCommandType_ThrowsArgumentException(string commandType) => Should.Throw<ArgumentException>(() =>
            new CommandEnvelope(
                MessageId: "msg-7",
                TenantId: "acme",
                Domain: "payments",
                AggregateId: "order-123",
                CommandType: commandType,
                Payload: [1],
                CorrelationId: "c1",
                CausationId: null,
                UserId: "u1",
                Extensions: null));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WithInvalidCorrelationId_ThrowsArgumentException(string correlationId) => Should.Throw<ArgumentException>(() =>
            new CommandEnvelope(
                MessageId: "msg-8",
                TenantId: "acme",
                Domain: "payments",
                AggregateId: "order-123",
                CommandType: "CreateOrder",
                Payload: [1],
                CorrelationId: correlationId,
                CausationId: null,
                UserId: "u1",
                Extensions: null));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WithInvalidUserId_ThrowsArgumentException(string userId) => Should.Throw<ArgumentException>(() =>
            new CommandEnvelope(
                MessageId: "msg-9",
                TenantId: "acme",
                Domain: "payments",
                AggregateId: "order-123",
                CommandType: "CreateOrder",
                Payload: [1],
                CorrelationId: "c1",
                CausationId: null,
                UserId: userId,
                Extensions: null));

    [Fact]
    public void Constructor_WithInvalidTenantId_ThrowsArgumentException() => Should.Throw<ArgumentException>(() =>
            new CommandEnvelope(
                MessageId: "msg-10",
                TenantId: "INVALID TENANT!",
                Domain: "payments",
                AggregateId: "order-123",
                CommandType: "CreateOrder",
                Payload: [1],
                CorrelationId: "c1",
                CausationId: null,
                UserId: "u1",
                Extensions: null));

    [Fact]
    public void AggregateIdentity_IsEagerlyValidated() =>
        // AggregateIdentity is computed eagerly at construction, not lazily on access
        Should.Throw<ArgumentException>(() =>
            new CommandEnvelope(
                MessageId: "msg-11",
                TenantId: "",
                Domain: "payments",
                AggregateId: "order-123",
                CommandType: "CreateOrder",
                Payload: [1],
                CorrelationId: "c1",
                CausationId: null,
                UserId: "u1",
                Extensions: null));

    [Fact]
    public void Extensions_DefensiveCopy_PreventsMutation() {
        var dict = new Dictionary<string, string> { ["key"] = "original" };
        var envelope = new CommandEnvelope(
            MessageId: "msg-12",
            TenantId: "acme",
            Domain: "payments",
            AggregateId: "order-123",
            CommandType: "CreateOrder",
            Payload: [1],
            CorrelationId: "c1",
            CausationId: null,
            UserId: "u1",
            Extensions: dict);

        dict["key"] = "mutated";

        envelope.Extensions!["key"].ShouldBe("original");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WithInvalidMessageId_ThrowsArgumentException(string messageId) => Should.Throw<ArgumentException>(() =>
            new CommandEnvelope(
                MessageId: messageId,
                TenantId: "acme",
                Domain: "payments",
                AggregateId: "order-123",
                CommandType: "CreateOrder",
                Payload: [1],
                CorrelationId: "c1",
                CausationId: null,
                UserId: "u1",
                Extensions: null));

    [Fact]
    public void ToString_ContainsMessageId() {
        var envelope = new CommandEnvelope(
            MessageId: "msg-tostring",
            TenantId: "acme",
            Domain: "payments",
            AggregateId: "order-123",
            CommandType: "CreateOrder",
            Payload: [1],
            CorrelationId: "c1",
            CausationId: null,
            UserId: "u1",
            Extensions: null);

        string result = envelope.ToString();

        result.ShouldContain("MessageId = msg-tostring");
        result.ShouldContain("[REDACTED]");
    }

    [Fact]
    public void DataContract_SerializationRoundTrip_PreservesMessageId() {
        var original = new CommandEnvelope(
            MessageId: "msg-serial",
            TenantId: "acme",
            Domain: "payments",
            AggregateId: "order-123",
            CommandType: "CreateOrder",
            Payload: [1, 2, 3],
            CorrelationId: "corr-serial",
            CausationId: "cause-serial",
            UserId: "user-serial",
            Extensions: null);

        var serializer = new System.Runtime.Serialization.DataContractSerializer(typeof(CommandEnvelope));
        using var ms = new System.IO.MemoryStream();
        serializer.WriteObject(ms, original);
        ms.Position = 0;
        var deserialized = (CommandEnvelope)serializer.ReadObject(ms)!;

        deserialized.MessageId.ShouldBe(original.MessageId);
        deserialized.TenantId.ShouldBe(original.TenantId);
        deserialized.Domain.ShouldBe(original.Domain);
        deserialized.AggregateId.ShouldBe(original.AggregateId);
        deserialized.CommandType.ShouldBe(original.CommandType);
        deserialized.CorrelationId.ShouldBe(original.CorrelationId);
        deserialized.CausationId.ShouldBe(original.CausationId);
        deserialized.UserId.ShouldBe(original.UserId);
    }
}
