using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Streams;

public class TimelineEntryTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var entry = new TimelineEntry(1, DateTimeOffset.UtcNow, TimelineEntryType.Event, "OrderCreated", "corr-001", "user-1");

        entry.SequenceNumber.ShouldBe(1);
        entry.EntryType.ShouldBe(TimelineEntryType.Event);
        entry.TypeName.ShouldBe("OrderCreated");
        entry.CorrelationId.ShouldBe("corr-001");
        entry.UserId.ShouldBe("user-1");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidTypeName_ThrowsArgumentException(string? typeName)
    {
        Should.Throw<ArgumentException>(() =>
            new TimelineEntry(1, DateTimeOffset.UtcNow, TimelineEntryType.Event, typeName!, "corr-001", null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidCorrelationId_ThrowsArgumentException(string? correlationId)
    {
        Should.Throw<ArgumentException>(() =>
            new TimelineEntry(1, DateTimeOffset.UtcNow, TimelineEntryType.Event, "OrderCreated", correlationId!, null));
    }

    [Fact]
    public void ToString_DoesNotExposePayloadData()
    {
        var entry = new TimelineEntry(1, DateTimeOffset.UtcNow, TimelineEntryType.Event, "OrderCreated", "corr-001", "user-1");

        string result = entry.ToString();

        result.ShouldContain("OrderCreated");
        result.ShouldContain("corr-001");
    }
}
