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

        Assert.Equal("msg-1", envelope.MessageId);
        Assert.Equal("acme", envelope.TenantId);
        Assert.Equal("payments", envelope.Domain);
        Assert.Equal("order-123", envelope.AggregateId);
        Assert.Equal("CreateOrder", envelope.CommandType);
        Assert.Same(payload, envelope.Payload);
        Assert.Equal("corr-1", envelope.CorrelationId);
        Assert.Null(envelope.CausationId);
        Assert.Equal("user-1", envelope.UserId);
        Assert.Null(envelope.Extensions);
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

        Assert.Equal("acme", identity.TenantId);
        Assert.Equal("payments", identity.Domain);
        Assert.Equal("order-123", identity.AggregateId);
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

        Assert.Null(envelope.Extensions);
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

        Assert.NotNull(envelope.Extensions);
        Assert.Equal("value", envelope.Extensions["key"]);
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

        Assert.Equal("cause-1", envelope.CausationId);
    }

    [Fact]
    public void Constructor_WithNullPayload_ThrowsArgumentNullException() => Assert.Throws<ArgumentNullException>(() =>
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
    public void Constructor_WithInvalidCommandType_ThrowsArgumentException(string commandType) => Assert.Throws<ArgumentException>(() =>
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
    public void Constructor_WithInvalidCorrelationId_ThrowsArgumentException(string correlationId) => Assert.Throws<ArgumentException>(() =>
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
    public void Constructor_WithInvalidUserId_ThrowsArgumentException(string userId) => Assert.Throws<ArgumentException>(() =>
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
    public void Constructor_WithInvalidTenantId_ThrowsArgumentException() => Assert.Throws<ArgumentException>(() =>
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
        Assert.Throws<ArgumentException>(() =>
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

        Assert.Equal("original", envelope.Extensions!["key"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WithInvalidMessageId_ThrowsArgumentException(string messageId) => Assert.Throws<ArgumentException>(() =>
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

        Assert.Contains("MessageId = msg-tostring", result);
        Assert.Contains("[REDACTED]", result);
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

        Assert.Equal(original.MessageId, deserialized.MessageId);
        Assert.Equal(original.TenantId, deserialized.TenantId);
        Assert.Equal(original.Domain, deserialized.Domain);
        Assert.Equal(original.AggregateId, deserialized.AggregateId);
        Assert.Equal(original.CommandType, deserialized.CommandType);
        Assert.Equal(original.CorrelationId, deserialized.CorrelationId);
        Assert.Equal(original.CausationId, deserialized.CausationId);
        Assert.Equal(original.UserId, deserialized.UserId);
    }
}
