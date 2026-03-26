using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Dapr;

public class DaprActorRuntimeInfoTests
{
    private static DaprActorRuntimeConfig DefaultConfig => new(
        TimeSpan.FromMinutes(60),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60),
        true,
        false,
        32);

    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        List<DaprActorTypeInfo> types =
        [
            new("AggregateActor", 10, "Desc", "format"),
            new("ETagActor", 5, "Desc2", "format2"),
        ];

        var info = new DaprActorRuntimeInfo(types, 15, DefaultConfig, true);

        info.ActorTypes.Count.ShouldBe(2);
        info.TotalActiveActors.ShouldBe(15);
        info.IsRemoteMetadataAvailable.ShouldBeTrue();
        info.Configuration.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_WithNullActorTypes_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DaprActorRuntimeInfo(null!, 0, DefaultConfig, false));
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DaprActorRuntimeInfo([], 0, null!, false));
    }

    [Fact]
    public void Constructor_WithEmptyActorTypes_CreatesInstance()
    {
        var info = new DaprActorRuntimeInfo([], 0, DefaultConfig, false);

        info.ActorTypes.ShouldBeEmpty();
        info.TotalActiveActors.ShouldBe(0);
        info.IsRemoteMetadataAvailable.ShouldBeFalse();
    }
}
