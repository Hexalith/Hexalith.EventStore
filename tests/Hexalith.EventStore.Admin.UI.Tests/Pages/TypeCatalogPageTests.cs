using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;
using Hexalith.EventStore.Admin.UI.Pages;
using Hexalith.EventStore.Admin.UI.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// bUnit tests for the TypeCatalog page.
/// </summary>
public class TypeCatalogPageTests : AdminUITestContext
{
    private readonly AdminTypeCatalogApiClient _mockApiClient;

    public TypeCatalogPageTests()
    {
        _mockApiClient = Substitute.For<AdminTypeCatalogApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminTypeCatalogApiClient>.Instance);
        Services.AddScoped(_ => _mockApiClient);
    }

    [Fact]
    public void TypeCatalogPage_RendersStatCards_WithCorrectCounts()
    {
        // Arrange
        SetupMockData();

        // Act
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Event Types"), TimeSpan.FromSeconds(5));

        // Assert — verify stat card labels and counts
        string markup = cut.Markup;
        markup.ShouldContain("Event Types");
        markup.ShouldContain("3"); // 3 events
        markup.ShouldContain("Command Types");
        markup.ShouldContain("2"); // 2 commands
        markup.ShouldContain("Aggregate Types");
        markup.ShouldContain("2 domains"); // subtitle
    }

    [Fact]
    public void TypeCatalogPage_RendersEventsGrid_WithAllColumns()
    {
        // Arrange
        SetupMockData();

        // Act
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("OrderCreated"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Type Name");
        markup.ShouldContain("Domain");
        markup.ShouldContain("Schema Version");
        markup.ShouldContain("Rejection");
        markup.ShouldContain("OrderCreated");
    }

    [Fact]
    public void TypeCatalogPage_RendersCommandsGrid_WithAllColumns()
    {
        // Arrange
        SetupMockData();

        // Act
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Event Types"), TimeSpan.FromSeconds(5));

        // Switch to commands tab
        IRenderedComponent<Microsoft.FluentUI.AspNetCore.Components.FluentTab> commandsTab = cut
            .FindComponents<Microsoft.FluentUI.AspNetCore.Components.FluentTab>()
            .First(t => t.Instance.Id == "commands");
        cut.InvokeAsync(() => cut.Instance.GetType()
            .GetMethod("OnTabChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(cut.Instance, ["commands"]));
        cut.Render();

        // Assert — commands grid content
        string markup = cut.Markup;
        markup.ShouldContain("Commands");
        markup.ShouldContain("Target Aggregate");
    }

    [Fact]
    public void TypeCatalogPage_RendersAggregatesGrid_WithAllColumns()
    {
        // Arrange
        SetupMockData();

        // Act
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Event Types"), TimeSpan.FromSeconds(5));

        // Switch to aggregates tab
        cut.InvokeAsync(() => cut.Instance.GetType()
            .GetMethod("OnTabChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(cut.Instance, ["aggregates"]));
        cut.Render();

        // Assert — aggregates grid content
        string markup = cut.Markup;
        markup.ShouldContain("Aggregates");
        markup.ShouldContain("Event Count");
        markup.ShouldContain("Command Count");
        markup.ShouldContain("Has Projections");
    }

    [Fact]
    public void TypeCatalogPage_ShowsIssueBanner_OnApiFailure()
    {
        // Arrange
        _ = _mockApiClient.ListEventTypesAsync(
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<EventTypeInfo>>(new HttpRequestException("Fail")));
        _ = _mockApiClient.ListCommandTypesAsync(
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<CommandTypeInfo>>(new HttpRequestException("Fail")));
        _ = _mockApiClient.ListAggregateTypesAsync(
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<AggregateTypeInfo>>(new HttpRequestException("Fail")));

        // Act
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Unable to load type catalog"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Unable to load type catalog");
    }

    [Fact]
    public void TypeCatalogPage_ShowsEmptyState_WhenNoTypesReturned()
    {
        // Arrange
        _ = _mockApiClient.ListEventTypesAsync(
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<EventTypeInfo>>([]));
        _ = _mockApiClient.ListCommandTypesAsync(
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CommandTypeInfo>>([]));
        _ = _mockApiClient.ListAggregateTypesAsync(
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AggregateTypeInfo>>([]));

        // Act
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No event types"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("No event types");
    }

    [Fact]
    public void TypeCatalogPage_RejectionBadge_RendersForRejectionEvents()
    {
        // Arrange
        SetupMockData();

        // Act
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("OrderCreated"), TimeSpan.FromSeconds(5));

        // Assert — rejection badge appears for the rejection event
        cut.Markup.ShouldContain("Rejection");
    }

    [Fact]
    public void TypeCatalogPage_HasProjectionsBadge_RendersCorrectly()
    {
        // Arrange
        SetupMockData();

        // Act
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Aggregate Types"), TimeSpan.FromSeconds(5));

        // Switch to aggregates tab
        cut.InvokeAsync(() => cut.Instance.GetType()
            .GetMethod("OnTabChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(cut.Instance, ["aggregates"]));
        cut.Render();

        // Assert — HasProjections badges rendered
        string markup = cut.Markup;
        markup.ShouldContain("Yes"); // OrderAggregate has projections
        markup.ShouldContain("No");  // PaymentAggregate does not
    }

    [Fact]
    public void TypeCatalogPage_GridHasAriaLabel()
    {
        // Arrange
        SetupMockData();

        // Act
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("OrderCreated"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Event types list");
    }

    [Fact]
    public void TypeCatalogPage_HasRefreshButton()
    {
        // Arrange
        SetupMockData();

        // Act
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Refresh"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Refresh");
    }

    [Fact]
    public void TypeCatalogPage_HasSearchInput()
    {
        // Arrange
        SetupMockData();

        // Act
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Search event types"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Search event types");
    }

    [Fact]
    public void TypeCatalogPage_HasDomainFilter()
    {
        // Arrange
        SetupMockData();

        // Act
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("All Domains"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("All Domains");
        cut.Markup.ShouldContain("ordering");
        cut.Markup.ShouldContain("payments");
    }

    [Fact]
    public void TypeCatalogPage_HasTabpanelRole()
    {
        // Arrange
        SetupMockData();

        // Act
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("OrderCreated"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("role=\"tabpanel\"");
    }

    [Fact]
    public void TypeCatalogPage_HasVisibleSubtitles()
    {
        // Arrange
        SetupMockData();

        // Act
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Event Types"), TimeSpan.FromSeconds(5));

        // Assert — subtitles are visible (not just tooltips)
        cut.Markup.ShouldContain("2 domains");
        cut.Markup.ShouldContain("1 with projections");
    }

    private void SetupMockData()
    {
        IReadOnlyList<EventTypeInfo> events =
        [
            new("OrderCreated", "ordering", false, 1),
            new("OrderRejected", "ordering", true, 1),
            new("PaymentProcessed", "payments", false, 2),
        ];
        IReadOnlyList<CommandTypeInfo> commands =
        [
            new("CreateOrder", "ordering", "OrderAggregate"),
            new("ProcessPayment", "payments", "PaymentAggregate"),
        ];
        IReadOnlyList<AggregateTypeInfo> aggregates =
        [
            new("OrderAggregate", "ordering", 2, 1, true),
            new("PaymentAggregate", "payments", 1, 1, false),
        ];

        _ = _mockApiClient.ListEventTypesAsync(
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(events));
        _ = _mockApiClient.ListCommandTypesAsync(
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(commands));
        _ = _mockApiClient.ListAggregateTypesAsync(
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(aggregates));
    }
}
