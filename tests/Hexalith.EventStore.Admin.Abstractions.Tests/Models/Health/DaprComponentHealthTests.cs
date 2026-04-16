using Hexalith.EventStore.Admin.Abstractions.Models.Health;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Health;

public class DaprComponentHealthTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        var health = new DaprComponentHealth("statestore", "state.redis", HealthStatus.Healthy, DateTimeOffset.UtcNow);

        health.ComponentName.ShouldBe("statestore");
        health.ComponentType.ShouldBe("state.redis");
        health.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidComponentName_ThrowsArgumentException(string? componentName) => Should.Throw<ArgumentException>(() =>
                                                                                                                new DaprComponentHealth(componentName!, "state.redis", HealthStatus.Healthy, DateTimeOffset.UtcNow));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidComponentType_ThrowsArgumentException(string? componentType) => Should.Throw<ArgumentException>(() =>
                                                                                                                new DaprComponentHealth("statestore", componentType!, HealthStatus.Healthy, DateTimeOffset.UtcNow));
}
