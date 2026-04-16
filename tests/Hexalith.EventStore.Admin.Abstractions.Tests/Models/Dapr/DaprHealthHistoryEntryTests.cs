using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Dapr;

public class DaprHealthHistoryEntryTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var entry = new DaprHealthHistoryEntry(
            "statestore",
            "state.redis",
            HealthStatus.Healthy,
            now);

        entry.ComponentName.ShouldBe("statestore");
        entry.ComponentType.ShouldBe("state.redis");
        entry.Status.ShouldBe(HealthStatus.Healthy);
        entry.CapturedAtUtc.ShouldBe(now);
    }

    [Fact]
    public void Constructor_WithNullComponentName_DefaultsToEmpty() {
        var entry = new DaprHealthHistoryEntry(
            null!,
            "state.redis",
            HealthStatus.Healthy,
            DateTimeOffset.UtcNow);

        entry.ComponentName.ShouldBe(string.Empty);
    }

    [Fact]
    public void Constructor_WithNullComponentType_DefaultsToEmpty() {
        var entry = new DaprHealthHistoryEntry(
            "statestore",
            null!,
            HealthStatus.Healthy,
            DateTimeOffset.UtcNow);

        entry.ComponentType.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData(HealthStatus.Healthy)]
    [InlineData(HealthStatus.Degraded)]
    [InlineData(HealthStatus.Unhealthy)]
    public void Constructor_WithAllHealthStatuses_CreatesInstance(HealthStatus status) {
        var entry = new DaprHealthHistoryEntry(
            "statestore",
            "state.redis",
            status,
            DateTimeOffset.UtcNow);

        entry.Status.ShouldBe(status);
    }
}
