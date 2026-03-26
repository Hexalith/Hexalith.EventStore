using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Dapr;

public class DaprActorTypeInfoTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var info = new DaprActorTypeInfo("AggregateActor", 42, "Processes commands", "tenant:domain:id");

        info.TypeName.ShouldBe("AggregateActor");
        info.ActiveCount.ShouldBe(42);
        info.Description.ShouldBe("Processes commands");
        info.ActorIdFormat.ShouldBe("tenant:domain:id");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidTypeName_ThrowsArgumentException(string? typeName)
    {
        Should.Throw<ArgumentException>(() =>
            new DaprActorTypeInfo(typeName!, 0, "Description", "format"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidDescription_ThrowsArgumentException(string? description)
    {
        Should.Throw<ArgumentException>(() =>
            new DaprActorTypeInfo("Type", 0, description!, "format"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidActorIdFormat_ThrowsArgumentException(string? format)
    {
        Should.Throw<ArgumentException>(() =>
            new DaprActorTypeInfo("Type", 0, "Description", format!));
    }

    [Fact]
    public void Constructor_WithNegativeOneActiveCount_CreatesInstance()
    {
        var info = new DaprActorTypeInfo("Type", -1, "Description", "format");

        info.ActiveCount.ShouldBe(-1);
    }

    [Fact]
    public void Constructor_WithActiveCountBelowNegativeOne_ThrowsArgumentOutOfRange()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new DaprActorTypeInfo("Type", -2, "Description", "format"));
    }

    [Fact]
    public void Constructor_WithZeroActiveCount_CreatesInstance()
    {
        var info = new DaprActorTypeInfo("Type", 0, "Description", "format");

        info.ActiveCount.ShouldBe(0);
    }
}
