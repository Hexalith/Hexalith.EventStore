using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Dapr;

public class DaprActorInstanceStateTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        List<DaprActorStateEntry> entries =
        [
            new("etag", "{\"v\":1}", 7, true),
            new("snapshot", null, 0, false),
        ];
        DateTimeOffset now = DateTimeOffset.UtcNow;

        var state = new DaprActorInstanceState("ETagActor", "Proj:Tenant1", entries, 7, now);

        state.ActorType.ShouldBe("ETagActor");
        state.ActorId.ShouldBe("Proj:Tenant1");
        state.StateEntries.Count.ShouldBe(2);
        state.TotalSizeBytes.ShouldBe(7);
        state.InspectedAtUtc.ShouldBe(now);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidActorType_ThrowsArgumentException(string? actorType) => Should.Throw<ArgumentException>(() =>
                                                                                                        new DaprActorInstanceState(actorType!, "id", [], 0, DateTimeOffset.UtcNow));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidActorId_ThrowsArgumentException(string? actorId) => Should.Throw<ArgumentException>(() =>
                                                                                                    new DaprActorInstanceState("Type", actorId!, [], 0, DateTimeOffset.UtcNow));

    [Fact]
    public void Constructor_WithNullStateEntries_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() =>
                                                                                           new DaprActorInstanceState("Type", "id", null!, 0, DateTimeOffset.UtcNow));

    [Fact]
    public void Constructor_WithEmptyEntries_CreatesInstance() {
        var state = new DaprActorInstanceState("Type", "id", [], 0, DateTimeOffset.UtcNow);

        state.StateEntries.ShouldBeEmpty();
        state.TotalSizeBytes.ShouldBe(0);
    }
}
