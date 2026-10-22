using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Streams;

public class CausationEventTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        var evt = new CausationEvent(1, "OrderCreated", DateTimeOffset.UtcNow);

        evt.SequenceNumber.ShouldBe(1);
        evt.EventTypeName.ShouldBe("OrderCreated");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidEventTypeName_ThrowsArgumentException(string? eventTypeName) => Should.Throw<ArgumentException>(() =>
                                                                                                                new CausationEvent(1, eventTypeName!, DateTimeOffset.UtcNow));
}
