using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.UI.Pages;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// bUnit tests for the DaprComponents page.
/// </summary>
public class DaprComponentsPageTests : AdminUITestContext {
    private readonly AdminDaprApiClient _mockApiClient;

    public DaprComponentsPageTests() {
        _mockApiClient = Substitute.For<AdminDaprApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminDaprApiClient>.Instance);
        _ = Services.AddScoped(_ => _mockApiClient);
    }

    // ===== Merge-blocking tests =====

    [Fact]
    public void DaprPage_RendersTitle() {
        // Arrange
        _ = _mockApiClient.GetSidecarInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprSidecarInfo?>(CreateSidecarInfo()));
        _ = _mockApiClient.GetComponentsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DaprComponentDetail>>(CreateComponents()));

        // Act
        IRenderedComponent<DaprComponents> cut = Render<DaprComponents>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("DAPR Infrastructure"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("DAPR Infrastructure");
    }

    [Fact]
    public void DaprPage_RendersStatCards_WithSidecarInfo() {
        // Arrange
        DaprSidecarInfo sidecar = CreateSidecarInfo();
        _ = _mockApiClient.GetSidecarInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprSidecarInfo?>(sidecar));
        _ = _mockApiClient.GetComponentsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DaprComponentDetail>>(CreateComponents()));

        // Act
        IRenderedComponent<DaprComponents> cut = Render<DaprComponents>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("App ID"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("App ID");
        markup.ShouldContain("Runtime Version");
        markup.ShouldContain("Components");
        markup.ShouldContain("Subscriptions");
        markup.ShouldContain("test-app");
        markup.ShouldContain("1.14.0");
    }

    [Fact]
    public void DaprPage_RendersComponentGrid() {
        // Arrange
        _ = _mockApiClient.GetSidecarInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprSidecarInfo?>(CreateSidecarInfo()));
        _ = _mockApiClient.GetComponentsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DaprComponentDetail>>(CreateComponents()));

        // Act
        IRenderedComponent<DaprComponents> cut = Render<DaprComponents>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("statestore"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("statestore");
        markup.ShouldContain("state.redis");
        markup.ShouldContain("pubsub");
        markup.ShouldContain("pubsub.kafka");
    }

    [Fact]
    public void DaprPage_RendersEmptyState_WhenSidecarUnavailable() {
        // Arrange
        _ = _mockApiClient.GetSidecarInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprSidecarInfo?>(null));
        _ = _mockApiClient.GetComponentsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DaprComponentDetail>>([]));

        // Act
        IRenderedComponent<DaprComponents> cut = Render<DaprComponents>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No DAPR components detected"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("No DAPR components detected");
    }

    [Fact]
    public void DaprPage_RendersRefreshButton() {
        // Arrange
        _ = _mockApiClient.GetSidecarInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprSidecarInfo?>(CreateSidecarInfo()));
        _ = _mockApiClient.GetComponentsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DaprComponentDetail>>(CreateComponents()));

        // Act
        IRenderedComponent<DaprComponents> cut = Render<DaprComponents>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Refresh"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Refresh");
    }

    [Fact]
    public void DaprPage_RendersIssueBanner_WhenApiUnavailable() {
        // Arrange
        _ = _mockApiClient.GetSidecarInfoAsync(Arg.Any<CancellationToken>())
            .Returns<DaprSidecarInfo?>(_ => throw new InvalidOperationException("API down"));
        _ = _mockApiClient.GetComponentsAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<DaprComponentDetail>>(_ => throw new InvalidOperationException("API down"));

        // Act
        IRenderedComponent<DaprComponents> cut = Render<DaprComponents>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Unable to load DAPR infrastructure"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Unable to load DAPR infrastructure");
    }

    [Fact]
    public void DaprPage_RendersFilterControls() {
        // Arrange
        _ = _mockApiClient.GetSidecarInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprSidecarInfo?>(CreateSidecarInfo()));
        _ = _mockApiClient.GetComponentsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DaprComponentDetail>>(CreateComponents()));

        // Act
        IRenderedComponent<DaprComponents> cut = Render<DaprComponents>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("All Categories"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("All Categories");
        markup.ShouldContain("State Store");
    }

    [Fact]
    public void DaprPage_RendersEmptyState_WhenSidecarUpButNoComponents() {
        // Arrange — sidecar available but zero components
        _ = _mockApiClient.GetSidecarInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprSidecarInfo?>(CreateSidecarInfo()));
        _ = _mockApiClient.GetComponentsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DaprComponentDetail>>([]));

        // Act
        IRenderedComponent<DaprComponents> cut = Render<DaprComponents>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No DAPR components detected"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("No DAPR components detected");
    }

    // ===== Helper methods =====

    private static DaprSidecarInfo CreateSidecarInfo() => new(
        "test-app",
        "1.14.0",
        3,
        2,
        1,
        RemoteMetadataStatus.Available,
        "http://localhost:3501");

    private static List<DaprComponentDetail> CreateComponents() =>
    [
        new DaprComponentDetail(
            "statestore",
            "state.redis",
            DaprComponentCategory.StateStore,
            "v1",
            HealthStatus.Healthy,
            DateTimeOffset.UtcNow,
            ["ETAG", "TRANSACTIONAL"]),
        new DaprComponentDetail(
            "pubsub",
            "pubsub.kafka",
            DaprComponentCategory.PubSub,
            "v2",
            HealthStatus.Healthy,
            DateTimeOffset.UtcNow,
            []),
    ];
}
