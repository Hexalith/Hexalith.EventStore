namespace Hexalith.EventStore.Contracts.Tests.Commands;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;

public class CommandEnvelopeTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        byte[] payload = [1, 2, 3];
        var envelope = new CommandEnvelope(
            TenantId: "acme",
            Domain: "payments",
            AggregateId: "order-123",
            CommandType: "CreateOrder",
            Payload: payload,
            CorrelationId: "corr-1",
            CausationId: null,
            UserId: "user-1",
            Extensions: null);

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
    public void AggregateIdentity_DerivesCorrectIdentity()
    {
        var envelope = new CommandEnvelope(
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
    public void Extensions_IsNullable()
    {
        var envelope = new CommandEnvelope("acme", "payments", "order-123", "CreateOrder", [1], "c1", null, "u1", null);

        Assert.Null(envelope.Extensions);
    }

    [Fact]
    public void Extensions_AcceptsReadOnlyDictionary()
    {
        var extensions = new Dictionary<string, string> { ["key"] = "value" }.AsReadOnly();
        var envelope = new CommandEnvelope("acme", "payments", "order-123", "CreateOrder", [1], "c1", null, "u1", extensions);

        Assert.NotNull(envelope.Extensions);
        Assert.Equal("value", envelope.Extensions["key"]);
    }

    [Fact]
    public void CausationId_IsNullable()
    {
        var envelope = new CommandEnvelope("acme", "payments", "order-123", "CreateOrder", [1], "c1", "cause-1", "u1", null);

        Assert.Equal("cause-1", envelope.CausationId);
    }

    [Fact]
    public void Constructor_WithNullPayload_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CommandEnvelope("acme", "payments", "order-123", "CreateOrder", null!, "c1", null, "u1", null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WithInvalidCommandType_ThrowsArgumentException(string commandType)
    {
        Assert.Throws<ArgumentException>(() =>
            new CommandEnvelope("acme", "payments", "order-123", commandType, [1], "c1", null, "u1", null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WithInvalidCorrelationId_ThrowsArgumentException(string correlationId)
    {
        Assert.Throws<ArgumentException>(() =>
            new CommandEnvelope("acme", "payments", "order-123", "CreateOrder", [1], correlationId, null, "u1", null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WithInvalidUserId_ThrowsArgumentException(string userId)
    {
        Assert.Throws<ArgumentException>(() =>
            new CommandEnvelope("acme", "payments", "order-123", "CreateOrder", [1], "c1", null, userId, null));
    }

    [Fact]
    public void Constructor_WithInvalidTenantId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new CommandEnvelope("INVALID TENANT!", "payments", "order-123", "CreateOrder", [1], "c1", null, "u1", null));
    }

    [Fact]
    public void AggregateIdentity_IsEagerlyValidated()
    {
        // AggregateIdentity is computed eagerly at construction, not lazily on access
        Assert.Throws<ArgumentException>(() =>
            new CommandEnvelope("", "payments", "order-123", "CreateOrder", [1], "c1", null, "u1", null));
    }

    [Fact]
    public void Extensions_DefensiveCopy_PreventsMutation()
    {
        var dict = new Dictionary<string, string> { ["key"] = "original" };
        var envelope = new CommandEnvelope("acme", "payments", "order-123", "CreateOrder", [1], "c1", null, "u1", dict);

        dict["key"] = "mutated";

        Assert.Equal("original", envelope.Extensions!["key"]);
    }
}
