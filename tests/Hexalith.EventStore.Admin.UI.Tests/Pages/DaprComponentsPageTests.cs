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
        SetupOverview(CreateSidecarInfo(), CreateComponents());

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
        SetupOverview(sidecar, CreateComponents());

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
        SetupOverview(CreateSidecarInfo(), CreateComponents());

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
    public void DaprPage_UsesSingleOverviewSnapshot() {
        SetupOverview(CreateSidecarInfo(), CreateComponents());

        IRenderedComponent<DaprComponents> cut = Render<DaprComponents>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("statestore"), TimeSpan.FromSeconds(5));

        _ = _mockApiClient.Received(1).GetInfrastructureOverviewAsync(Arg.Any<CancellationToken>());
        _ = _mockApiClient.DidNotReceive().GetSidecarInfoAsync(Arg.Any<CancellationToken>());
        _ = _mockApiClient.DidNotReceive().GetComponentsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DaprPage_RendersEmptyState_WhenSidecarUnavailable() {
        // After round-3 patch F23 the empty-state copy distinguishes "API returned null sidecar"
        // from a non-Available RemoteMetadataStatus, surfacing the actual unavailability cause.
        SetupOverview(null, []);

        IRenderedComponent<DaprComponents> cut = Render<DaprComponents>();
        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("DAPR sidecar metadata unavailable"),
            TimeSpan.FromSeconds(5));

        cut.Markup.ShouldContain("DAPR sidecar metadata unavailable");
    }

    [Fact]
    public void DaprPage_RendersComponentGrid_WhenSidecarInfoMissingButComponentsAvailable() {
        SetupOverview(null, CreateComponents());

        IRenderedComponent<DaprComponents> cut = Render<DaprComponents>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("statestore"), TimeSpan.FromSeconds(5));

        cut.Markup.ShouldContain("statestore");
        cut.Markup.ShouldContain("pubsub");
        cut.Markup.ShouldNotContain("DAPR sidecar metadata unavailable");
    }

    [Fact]
    public void DaprPage_RendersRefreshButton() {
        // Arrange
        SetupOverview(CreateSidecarInfo(), CreateComponents());

        // Act
        IRenderedComponent<DaprComponents> cut = Render<DaprComponents>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Refresh"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Refresh");
    }

    [Fact]
    public void DaprPage_RendersIssueBanner_WhenApiUnavailable() {
        // Arrange
        _ = _mockApiClient.GetInfrastructureOverviewAsync(Arg.Any<CancellationToken>())
            .Returns<DaprInfrastructureOverview?>(_ => throw new InvalidOperationException("API down"));

        // Act
        IRenderedComponent<DaprComponents> cut = Render<DaprComponents>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Unable to load DAPR infrastructure"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Unable to load DAPR infrastructure");
    }

    [Fact]
    public void DaprPage_RendersFilterControls() {
        // Arrange
        SetupOverview(CreateSidecarInfo(), CreateComponents());

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
        // Sidecar reachable AND remote metadata Available, but the canonical inventory has
        // zero components. Round 3 (F23) surfaces this as "No DAPR components reported" so
        // operators can distinguish "sidecar said zero" from the generic "no metadata at all"
        // case (which now renders different copy).
        SetupOverview(CreateSidecarInfo(), []);

        IRenderedComponent<DaprComponents> cut = Render<DaprComponents>();
        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("No DAPR components reported"),
            TimeSpan.FromSeconds(5));

        cut.Markup.ShouldContain("No DAPR components reported");
    }

    // ===== Helper methods =====

    private void SetupOverview(DaprSidecarInfo? sidecar, IReadOnlyList<DaprComponentDetail> components) => _ = _mockApiClient.GetInfrastructureOverviewAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprInfrastructureOverview?>(
                new DaprInfrastructureOverview(sidecar, components)));

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
