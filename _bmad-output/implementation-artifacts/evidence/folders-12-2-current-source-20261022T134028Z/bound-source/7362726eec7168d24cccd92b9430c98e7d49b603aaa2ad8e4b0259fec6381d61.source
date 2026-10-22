using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Dapr;

public class DaprSidecarInfoTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        var info = new DaprSidecarInfo("my-app", "1.14.0", 5, 3, 2, RemoteMetadataStatus.Available, "http://localhost:3501");

        info.AppId.ShouldBe("my-app");
        info.RuntimeVersion.ShouldBe("1.14.0");
        info.ComponentCount.ShouldBe(5);
        info.SubscriptionCount.ShouldBe(3);
        info.HttpEndpointCount.ShouldBe(2);
        info.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Available);
        info.RemoteEndpoint.ShouldBe("http://localhost:3501");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidAppId_ThrowsArgumentException(string? appId) => Should.Throw<ArgumentException>(() =>
                                                                                                new DaprSidecarInfo(appId!, "1.14.0", 5, 3, 2, RemoteMetadataStatus.Available, "http://localhost:3501"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidRuntimeVersion_ThrowsArgumentException(string? runtimeVersion) => Should.Throw<ArgumentException>(() =>
                                                                                                                  new DaprSidecarInfo("my-app", runtimeVersion!, 5, 3, 2, RemoteMetadataStatus.Available, "http://localhost:3501"));

    [Fact]
    public void Constructor_WithZeroCounts_CreatesInstance() {
        var info = new DaprSidecarInfo("my-app", "1.14.0", 0, 0, 0, RemoteMetadataStatus.NotConfigured, null);

        info.ComponentCount.ShouldBe(0);
        info.SubscriptionCount.ShouldBe(0);
        info.HttpEndpointCount.ShouldBe(0);
        info.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.NotConfigured);
        info.RemoteEndpoint.ShouldBeNull();
    }
}
