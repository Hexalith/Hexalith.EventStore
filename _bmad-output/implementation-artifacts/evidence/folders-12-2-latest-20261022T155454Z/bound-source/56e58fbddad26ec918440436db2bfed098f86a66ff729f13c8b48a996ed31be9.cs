using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Dapr;

public class DaprActorStateEntryTests {
    [Fact]
    public void Constructor_WithFoundEntry_CreatesInstance() {
        var entry = new DaprActorStateEntry("etag", "{\"value\":\"abc\"}", 17, true);

        entry.Key.ShouldBe("etag");
        entry.JsonValue.ShouldBe("{\"value\":\"abc\"}");
        entry.EstimatedSizeBytes.ShouldBe(17);
        entry.Found.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithNotFoundEntry_CreatesInstance() {
        var entry = new DaprActorStateEntry("snapshot", null, 0, false);

        entry.Key.ShouldBe("snapshot");
        entry.JsonValue.ShouldBeNull();
        entry.EstimatedSizeBytes.ShouldBe(0);
        entry.Found.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidKey_ThrowsArgumentException(string? key) => Should.Throw<ArgumentException>(() =>
                                                                                            new DaprActorStateEntry(key!, null, 0, false));
}
