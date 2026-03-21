using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.TypeCatalog;

public class CommandTypeInfoTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var info = new CommandTypeInfo("CreateOrder", "orders", "Order");

        info.TypeName.ShouldBe("CreateOrder");
        info.Domain.ShouldBe("orders");
        info.TargetAggregateType.ShouldBe("Order");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidTypeName_ThrowsArgumentException(string? typeName)
    {
        Should.Throw<ArgumentException>(() =>
            new CommandTypeInfo(typeName!, "orders", "Order"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidDomain_ThrowsArgumentException(string? domain)
    {
        Should.Throw<ArgumentException>(() =>
            new CommandTypeInfo("CreateOrder", domain!, "Order"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidTargetAggregateType_ThrowsArgumentException(string? targetAggregateType)
    {
        Should.Throw<ArgumentException>(() =>
            new CommandTypeInfo("CreateOrder", "orders", targetAggregateType!));
    }
}
