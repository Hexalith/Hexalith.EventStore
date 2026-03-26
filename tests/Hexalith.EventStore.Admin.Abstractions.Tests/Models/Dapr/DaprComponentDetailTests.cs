using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Dapr;

public class DaprComponentDetailTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var detail = new DaprComponentDetail(
            "statestore",
            "state.redis",
            DaprComponentCategory.StateStore,
            "v1",
            HealthStatus.Healthy,
            DateTimeOffset.UtcNow,
            ["ETAG", "TRANSACTIONAL"]);

        detail.ComponentName.ShouldBe("statestore");
        detail.ComponentType.ShouldBe("state.redis");
        detail.Category.ShouldBe(DaprComponentCategory.StateStore);
        detail.Version.ShouldBe("v1");
        detail.Status.ShouldBe(HealthStatus.Healthy);
        detail.Capabilities.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidComponentName_ThrowsArgumentException(string? componentName)
    {
        Should.Throw<ArgumentException>(() =>
            new DaprComponentDetail(
                componentName!,
                "state.redis",
                DaprComponentCategory.StateStore,
                "v1",
                HealthStatus.Healthy,
                DateTimeOffset.UtcNow,
                []));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidComponentType_ThrowsArgumentException(string? componentType)
    {
        Should.Throw<ArgumentException>(() =>
            new DaprComponentDetail(
                "statestore",
                componentType!,
                DaprComponentCategory.StateStore,
                "v1",
                HealthStatus.Healthy,
                DateTimeOffset.UtcNow,
                []));
    }

    [Fact]
    public void Constructor_WithEmptyVersion_CreatesInstance()
    {
        var detail = new DaprComponentDetail(
            "statestore",
            "state.redis",
            DaprComponentCategory.StateStore,
            string.Empty,
            HealthStatus.Healthy,
            DateTimeOffset.UtcNow,
            []);

        detail.Version.ShouldBe(string.Empty);
    }

    [Fact]
    public void Constructor_WithEmptyCapabilities_CreatesInstance()
    {
        var detail = new DaprComponentDetail(
            "statestore",
            "state.redis",
            DaprComponentCategory.StateStore,
            "v1",
            HealthStatus.Healthy,
            DateTimeOffset.UtcNow,
            []);

        detail.Capabilities.ShouldBeEmpty();
    }
}
