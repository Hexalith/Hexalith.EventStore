using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Dapr;

public class DaprActorRuntimeConfigTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var config = new DaprActorRuntimeConfig(
            TimeSpan.FromMinutes(60),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(60),
            true,
            false,
            32);

        config.IdleTimeout.ShouldBe(TimeSpan.FromMinutes(60));
        config.ScanInterval.ShouldBe(TimeSpan.FromSeconds(30));
        config.DrainOngoingCallTimeout.ShouldBe(TimeSpan.FromSeconds(60));
        config.DrainRebalancedActors.ShouldBeTrue();
        config.ReentrancyEnabled.ShouldBeFalse();
        config.ReentrancyMaxStackDepth.ShouldBe(32);
    }

    [Fact]
    public void Constructor_WithCustomValues_CreatesInstance()
    {
        var config = new DaprActorRuntimeConfig(
            TimeSpan.FromMinutes(120),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(90),
            false,
            true,
            64);

        config.IdleTimeout.ShouldBe(TimeSpan.FromMinutes(120));
        config.ReentrancyEnabled.ShouldBeTrue();
        config.ReentrancyMaxStackDepth.ShouldBe(64);
    }
}
