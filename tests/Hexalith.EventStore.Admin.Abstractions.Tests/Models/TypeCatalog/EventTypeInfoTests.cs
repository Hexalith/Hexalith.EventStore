using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.TypeCatalog;

public class EventTypeInfoTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var info = new EventTypeInfo("OrderCreated", "orders", false, 1);

        info.TypeName.ShouldBe("OrderCreated");
        info.Domain.ShouldBe("orders");
        info.IsRejection.ShouldBeFalse();
        info.SchemaVersion.ShouldBe(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidTypeName_ThrowsArgumentException(string? typeName)
    {
        Should.Throw<ArgumentException>(() =>
            new EventTypeInfo(typeName!, "orders", false, 1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidDomain_ThrowsArgumentException(string? domain)
    {
        Should.Throw<ArgumentException>(() =>
            new EventTypeInfo("OrderCreated", domain!, false, 1));
    }
}
