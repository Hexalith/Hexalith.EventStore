using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.UI.Pages;
using Hexalith.EventStore.Admin.UI.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// bUnit tests for the DaprPubSub page.
/// </summary>
public class DaprPubSubPageTests : AdminUITestContext
{
    private readonly AdminPubSubApiClient _mockPubSubClient;
    private readonly AdminDeadLetterApiClient _mockDeadLetterClient;
    private readonly AdminStreamApiClient _mockStreamClient;

    public DaprPubSubPageTests()
    {
        _mockPubSubClient = Substitute.For<AdminPubSubApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminPubSubApiClient>.Instance);
        _mockDeadLetterClient = Substitute.For<AdminDeadLetterApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminDeadLetterApiClient>.Instance);
        _mockStreamClient = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);
        Services.AddScoped(_ => _mockPubSubClient);
        Services.AddScoped(_ => _mockDeadLetterClient);
        Services.AddScoped(_ => _mockStreamClient);
    }

    [Fact]
    public void PubSubPage_RendersTitle()
    {
        // Arrange
        SetupSuccessfulResponse();

        // Act
        IRenderedComponent<DaprPubSub> cut = Render<DaprPubSub>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Pub/Sub Delivery Metrics"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Pub/Sub Delivery Metrics");
    }

    [Fact]
    public void PubSubPage_RendersBackLink()
    {
        // Arrange
        SetupSuccessfulResponse();

        // Act
        IRenderedComponent<DaprPubSub> cut = Render<DaprPubSub>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("DAPR Infrastructure"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("DAPR Infrastructure");
    }

    [Fact]
    public void PubSubPage_RendersStatCards_WithOverviewData()
    {
        // Arrange
        SetupSuccessfulResponse();

        // Act
        IRenderedComponent<DaprPubSub> cut = Render<DaprPubSub>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Pub/Sub Components"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Pub/Sub Components");
        markup.ShouldContain("Active Subscriptions");
        markup.ShouldContain("Unique Topics");
        markup.ShouldContain("Dead Letters");
    }

    [Fact]
    public void PubSubPage_RendersEmptyState_WhenNoPubSubComponents()
    {
        // Arrange
        DaprPubSubOverview overview = new([], [], RemoteMetadataStatus.Available, "http://localhost:3501");
        _ = _mockPubSubClient.GetPubSubOverviewAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprPubSubOverview?>(overview));
        _ = _mockDeadLetterClient.GetDeadLetterCountAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<int?>(0));

        // Act
        IRenderedComponent<DaprPubSub> cut = Render<DaprPubSub>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No pub/sub components found"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("No pub/sub components found");
    }

    [Fact]
    public void PubSubPage_RendersIssueBanner_WhenRemoteMetadataUnavailable()
    {
        // Arrange
        DaprPubSubOverview overview = new(
            [CreatePubSubComponent()],
            [],
            RemoteMetadataStatus.Unreachable,
            "http://localhost:3501");
        _ = _mockPubSubClient.GetPubSubOverviewAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprPubSubOverview?>(overview));
        _ = _mockDeadLetterClient.GetDeadLetterCountAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<int?>(0));

        // Act
        IRenderedComponent<DaprPubSub> cut = Render<DaprPubSub>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("EventStore sidecar unreachable"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("EventStore sidecar unreachable");
    }

    [Fact]
    public void PubSubPage_RendersSubscriptionGrid_WithData()
    {
        // Arrange
        SetupSuccessfulResponse();

        // Act
        IRenderedComponent<DaprPubSub> cut = Render<DaprPubSub>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("*.*.events"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("*.*.events");
        markup.ShouldContain("/events/handle");
        markup.ShouldContain("DECLARATIVE");
    }

    [Fact]
    public void PubSubPage_RendersComponentCards()
    {
        // Arrange
        SetupSuccessfulResponse();

        // Act
        IRenderedComponent<DaprPubSub> cut = Render<DaprPubSub>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("pubsub-events"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("pubsub-events");
        markup.ShouldContain("pubsub.redis");
    }

    [Fact]
    public void PubSubPage_RendersIssueBanner_WhenApiUnavailable()
    {
        // Arrange
        _ = _mockPubSubClient.GetPubSubOverviewAsync(Arg.Any<CancellationToken>())
            .Returns<DaprPubSubOverview?>(_ => throw new InvalidOperationException("API down"));
        _ = _mockDeadLetterClient.GetDeadLetterCountAsync(Arg.Any<CancellationToken>())
            .Returns<int?>(_ => throw new InvalidOperationException("API down"));

        // Act
        IRenderedComponent<DaprPubSub> cut = Render<DaprPubSub>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Unable to load pub/sub information"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Unable to load pub/sub information");
    }

    [Fact]
    public void PubSubPage_RendersDeadLetterManagementCard()
    {
        // Arrange
        SetupSuccessfulResponse();

        // Act
        IRenderedComponent<DaprPubSub> cut = Render<DaprPubSub>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Manage Dead Letters"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Dead Letter Management");
        cut.Markup.ShouldContain("Manage Dead Letters");
    }

    [Fact]
    public void PubSubPage_RendersRefreshButton()
    {
        // Arrange
        SetupSuccessfulResponse();

        // Act
        IRenderedComponent<DaprPubSub> cut = Render<DaprPubSub>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Refresh"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Refresh");
    }

    [Fact]
    public void PubSubPage_RendersObservabilityLinks_WhenUrlsConfigured()
    {
        // Arrange
        SetupSuccessfulResponse();
        ObservabilityLinks links = new("https://traces.example.com", "https://metrics.example.com", "https://logs.example.com");
        SystemHealthReport health = new(HealthStatus.Healthy, 100, 5.0, 0.1, [], links);
        _ = _mockStreamClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(health));

        // Act
        IRenderedComponent<DaprPubSub> cut = Render<DaprPubSub>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Open Trace Dashboard"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Open Trace Dashboard");
        cut.Markup.ShouldContain("Open Metrics Dashboard");
        cut.Markup.ShouldContain("Open Logs Dashboard");
    }

    // ===== Helper methods =====

    private void SetupSuccessfulResponse()
    {
        DaprPubSubOverview overview = new(
            [CreatePubSubComponent()],
            [new DaprSubscriptionInfo("pubsub-events", "*.*.events", "/events/handle", "DECLARATIVE", null)],
            RemoteMetadataStatus.Available,
            "http://localhost:3501");
        _ = _mockPubSubClient.GetPubSubOverviewAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprPubSubOverview?>(overview));
        _ = _mockDeadLetterClient.GetDeadLetterCountAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<int?>(3));
    }

    private static DaprComponentDetail CreatePubSubComponent() => new(
        "pubsub-events",
        "pubsub.redis",
        DaprComponentCategory.PubSub,
        "v1",
        HealthStatus.Healthy,
        DateTimeOffset.UtcNow,
        []);
}
