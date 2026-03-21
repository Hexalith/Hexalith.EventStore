using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.TypeCatalog;

public class AggregateTypeInfoTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var info = new AggregateTypeInfo("Order", "orders", 5, 3, true);

        info.TypeName.ShouldBe("Order");
        info.Domain.ShouldBe("orders");
        info.EventCount.ShouldBe(5);
        info.CommandCount.ShouldBe(3);
        info.HasProjections.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidTypeName_ThrowsArgumentException(string? typeName)
    {
        Should.Throw<ArgumentException>(() =>
            new AggregateTypeInfo(typeName!, "orders", 0, 0, false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidDomain_ThrowsArgumentException(string? domain)
    {
        Should.Throw<ArgumentException>(() =>
            new AggregateTypeInfo("Order", domain!, 0, 0, false));
    }
}
