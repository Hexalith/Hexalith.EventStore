using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;
using Hexalith.EventStore.Admin.UI.Pages;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// bUnit tests for the TypeCatalog page.
/// </summary>
public class TypeCatalogPageTests : AdminUITestContext {
    private readonly AdminTypeCatalogApiClient _mockApiClient;

    public TypeCatalogPageTests() {
        _mockApiClient = Substitute.For<AdminTypeCatalogApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminTypeCatalogApiClient>.Instance);
        _ = Services.AddScoped(_ => _mockApiClient);
    }

    [Fact]
    public void TypeCatalogPage_RendersStatCards_WithCorrectCounts() {
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
    public void TypeCatalogPage_RendersEventsGrid_WithAllColumns() {
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
    public void TypeCatalogPage_RendersCommandsGrid_WithAllColumns() {
        // Arrange
        SetupMockData();

        // Act
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Event Types"), TimeSpan.FromSeconds(5));

        // Switch to commands tab
        IRenderedComponent<Microsoft.FluentUI.AspNetCore.Components.FluentTab> commandsTab = cut
            .FindComponents<Microsoft.FluentUI.AspNetCore.Components.FluentTab>()
            .First(t => t.Instance.Id == "commands");
        _ = cut.InvokeAsync(() => cut.Instance.GetType()
            .GetMethod("OnTabChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(cut.Instance, ["commands"]));
        cut.Render();

        // Assert — commands grid content
        string markup = cut.Markup;
        markup.ShouldContain("Commands");
        markup.ShouldContain("Target Aggregate");
    }

    [Fact]
    public void TypeCatalogPage_RendersAggregatesGrid_WithAllColumns() {
        // Arrange
        SetupMockData();

        // Act
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Event Types"), TimeSpan.FromSeconds(5));

        // Switch to aggregates tab
        _ = cut.InvokeAsync(() => cut.Instance.GetType()
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
    public void TypeCatalogPage_ShowsIssueBanner_OnApiFailure() {
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
    public void TypeCatalogPage_ShowsEmptyState_WhenNoTypesReturned() {
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
    public void TypeCatalogPage_RejectionBadge_RendersForRejectionEvents() {
        // Arrange
        SetupMockData();

        // Act
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("OrderCreated"), TimeSpan.FromSeconds(5));

        // Assert — rejection badge appears for the rejection event
        cut.Markup.ShouldContain("Rejection");
    }

    [Fact]
    public void TypeCatalogPage_HasProjectionsBadge_RendersCorrectly() {
        // Arrange
        SetupMockData();

        // Act
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Aggregate Types"), TimeSpan.FromSeconds(5));

        // Switch to aggregates tab
        _ = cut.InvokeAsync(() => cut.Instance.GetType()
            .GetMethod("OnTabChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(cut.Instance, ["aggregates"]));
        cut.Render();

        // Assert — HasProjections badges rendered
        string markup = cut.Markup;
        markup.ShouldContain("Yes"); // OrderAggregate has projections
        markup.ShouldContain("No");  // PaymentAggregate does not
    }

    [Fact]
    public void TypeCatalogPage_GridHasAriaLabel() {
        // Arrange
        SetupMockData();

        // Act
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("OrderCreated"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Event types list");
    }

    [Fact]
    public void TypeCatalogPage_HasRefreshButton() {
        // Arrange
        SetupMockData();

        // Act
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Refresh"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Refresh");
    }

    [Fact]
    public void TypeCatalogPage_HasSearchInput() {
        // Arrange
        SetupMockData();

        // Act
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Search event types"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Search event types");
    }

    [Fact]
    public void TypeCatalogPage_HasDomainFilter() {
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
    public void TypeCatalogPage_HasTabpanelRole() {
        // Arrange
        SetupMockData();

        // Act
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("OrderCreated"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("role=\"tabpanel\"");
    }

    [Fact]
    public void TypeCatalogPage_HasVisibleSubtitles() {
        // Arrange
        SetupMockData();

        // Act
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Event Types"), TimeSpan.FromSeconds(5));

        // Assert — subtitles are visible (not just tooltips)
        cut.Markup.ShouldContain("2 domains");
        cut.Markup.ShouldContain("1 with projections");
    }

    [Fact]
    public void TypeCatalogPage_DeepLink_SetsActiveTabToAggregatesWithoutRedirectLoop() {
        // Regression test for Story 21-13 Bug #2 (TypeCatalog redirect loop on /types?tab=aggregates).
        // The fix is an idempotency guard in UpdateUrl that short-circuits NavigateTo when the
        // target URL already matches the current URL. Starting with a URL that already has
        // ?tab=aggregates proves the guard prevents the replace-navigation loop.
        SetupMockData();

        Microsoft.AspNetCore.Components.NavigationManager nav = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        nav.NavigateTo("/types?tab=aggregates");

        int navigationCount = 0;
        nav.LocationChanged += (_, _) => navigationCount++;

        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Aggregates"), TimeSpan.FromSeconds(5));

        // Active tab should be "aggregates" after reading URL params in OnInitializedAsync.
        System.Reflection.FieldInfo activeTabField = typeof(TypeCatalog)
            .GetField("_activeTab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        ((string?)activeTabField.GetValue(cut.Instance)).ShouldBe("aggregates");

        // The URL already matches — UpdateUrl's idempotency guard must have short-circuited
        // every scheduled NavigateTo during render. No LocationChanged should have fired.
        navigationCount.ShouldBe(0);
    }

    [Fact]
    public async Task TypeCatalogPage_UpdateUrl_IsIdempotent_WhenTargetEqualsCurrentUrl() {
        // Lower-level regression test: invoking UpdateUrl() when the target URL already
        // matches NavigationManager.Uri must NOT trigger LocationChanged.
        SetupMockData();

        Microsoft.AspNetCore.Components.NavigationManager nav = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();

        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Event Types"), TimeSpan.FromSeconds(5));

        // Navigate to a URL that matches the default state (tab=events means no query — so /types bare).
        nav.NavigateTo("/types");

        int navigationCount = 0;
        nav.LocationChanged += (_, _) => navigationCount++;

        // Invoke UpdateUrl via reflection — since state is default, target URL == "/types" == current.
        System.Reflection.MethodInfo updateUrl = typeof(TypeCatalog)
            .GetMethod("UpdateUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        await cut.InvokeAsync(() =>
        {
            _ = updateUrl.Invoke(cut.Instance, new object?[] { false });
        });

        cut.WaitForAssertion(() => navigationCount.ShouldBe(0), TimeSpan.FromMilliseconds(200));
    }

    private void SetupMockData() {
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
