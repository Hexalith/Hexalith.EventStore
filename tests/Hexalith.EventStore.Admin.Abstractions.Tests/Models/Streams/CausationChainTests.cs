using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Streams;

public class CausationChainTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var events = new List<CausationEvent>
        {
            new(1, "OrderCreated", DateTimeOffset.UtcNow),
        };
        var projections = new List<string> { "OrderSummary" };

        var chain = new CausationChain("CreateOrder", "cmd-001", "corr-001", "user-1", events, projections);

        chain.OriginatingCommandType.ShouldBe("CreateOrder");
        chain.OriginatingCommandId.ShouldBe("cmd-001");
        chain.CorrelationId.ShouldBe("corr-001");
        chain.UserId.ShouldBe("user-1");
        chain.Events.ShouldHaveSingleItem();
        chain.AffectedProjections.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidOriginatingCommandType_ThrowsArgumentException(string? value)
    {
        Should.Throw<ArgumentException>(() =>
            new CausationChain(value!, "cmd-001", "corr-001", null, [], []));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidOriginatingCommandId_ThrowsArgumentException(string? value)
    {
        Should.Throw<ArgumentException>(() =>
            new CausationChain("CreateOrder", value!, "corr-001", null, [], []));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidCorrelationId_ThrowsArgumentException(string? value)
    {
        Should.Throw<ArgumentException>(() =>
            new CausationChain("CreateOrder", "cmd-001", value!, null, [], []));
    }

    [Fact]
    public void Constructor_WithNullEvents_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new CausationChain("CreateOrder", "cmd-001", "corr-001", null, null!, []));
    }

    [Fact]
    public void Constructor_WithNullAffectedProjections_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new CausationChain("CreateOrder", "cmd-001", "corr-001", null, [], null!));
    }

    [Fact]
    public void Constructor_WithNullUserId_Succeeds()
    {
        var chain = new CausationChain("CreateOrder", "cmd-001", "corr-001", null, [], []);

        chain.UserId.ShouldBeNull();
    }
}
