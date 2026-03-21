using Hexalith.EventStore.Admin.Abstractions.Models.Projections;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Projections;

public class ProjectionErrorTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var error = new ProjectionError(100, DateTimeOffset.UtcNow, "Deserialization failed", "OrderCreated");

        error.Position.ShouldBe(100);
        error.Message.ShouldBe("Deserialization failed");
        error.EventTypeName.ShouldBe("OrderCreated");
    }

    [Fact]
    public void Constructor_WithNullEventTypeName_CreatesInstance()
    {
        var error = new ProjectionError(100, DateTimeOffset.UtcNow, "Unknown error", null);

        error.EventTypeName.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidMessage_ThrowsArgumentException(string? message)
    {
        Should.Throw<ArgumentException>(() =>
            new ProjectionError(100, DateTimeOffset.UtcNow, message!, null));
    }
}
