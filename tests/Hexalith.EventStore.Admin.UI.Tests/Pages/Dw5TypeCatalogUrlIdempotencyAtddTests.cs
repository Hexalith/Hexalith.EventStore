using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;
using Hexalith.EventStore.Admin.UI.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

// ATDD red-phase scaffolds for story:
//   _bmad-output/implementation-artifacts/post-epic-deferred-dw5-admin-ui-runtime-follow-ups.md
//
// AC#4 — TypeCatalog URL and selection behavior MUST remain non-regressive. The
//        following deep-link forms are existing user-visible behavior and must keep
//        working through any DW5 navigation/render-loop fix:
//          /types
//          /types?tab=commands
//          /types?tab=aggregates
//          /types?tab=events&type={typeName}
//        The story explicitly forbids reintroducing the previous redirect loop.
//
// These bUnit scaffolds verify that the page initializes the expected tab when arriving
// from each URL form. They also pin that UpdateUrl normalizes the empty/default tab to
// /types (no `tab=events` in the query string) so deep links remain stable.
public class Dw5TypeCatalogUrlIdempotencyAtddTests : AdminUITestContext {
    private readonly AdminTypeCatalogApiClient _mockApiClient;

    public Dw5TypeCatalogUrlIdempotencyAtddTests() {
        _mockApiClient = Substitute.For<AdminTypeCatalogApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminTypeCatalogApiClient>.Instance);
        SeedSampleTypes();
        _ = Services.AddScoped(_ => _mockApiClient);
    }

    [Fact]
    public void DeepLink_Default_InitializesEventsTab() {
        // AC#4 — /types must land on the events tab by default and render the
        // events-tab marker (event type name from the seeded data).
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/types", replace: true);

        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("OrderCreated"),
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void DeepLink_TabCommands_InitializesCommandsTab() {
        // AC#4 — /types?tab=commands must land on commands and render the seeded command name.
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/types?tab=commands", replace: true);

        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("CreateOrder"),
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void DeepLink_TabAggregates_InitializesAggregatesTab() {
        // AC#4 — /types?tab=aggregates must land on aggregates and render the seeded aggregate name.
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/types?tab=aggregates", replace: true);

        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("OrderAggregate"),
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void DeepLink_TypeSelection_InitializesSelection() {
        // AC#4 — /types?type=OrderCreated must SELECT OrderCreated on the matching tab,
        // not merely list it. We seed two events so a non-selecting render shows both;
        // a selection-only marker (the selected type's name appearing in a detail panel
        // header or as a highlighted selection indicator) is the deterministic signal.
        // We use the URL deep-link's selection contract: the selected type must appear
        // outside the table (e.g. in a detail/inspector panel marker).
        IReadOnlyList<EventTypeInfo> events = [
            new("OrderCreated", "ordering", false, 1),
            new("OrderShipped", "ordering", false, 1),
        ];
        _ = _mockApiClient.ListEventTypesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(events));

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/types?type=OrderCreated", replace: true);

        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("OrderCreated"),
            TimeSpan.FromSeconds(5));

        // The selected type's name must appear at least twice: once in the listing row
        // (rendered alongside OrderShipped) and once as a selection indicator (detail
        // panel header, highlight, breadcrumb). This catches a regression where the
        // URL parameter is parsed but no selection state is applied.
        int occurrences = System.Text.RegularExpressions.Regex
            .Matches(cut.Markup, "OrderCreated")
            .Count;
        occurrences.ShouldBeGreaterThan(1,
            customMessage: "DW5 AC#4: /types?type=OrderCreated must produce a selection-only marker in addition to the listing row.");
    }

    [Fact]
    public async Task UpdateUrl_DefaultTab_DoesNotEmitTabQueryString() {
        // AC#4 — When the events tab is active and no filters are set, UpdateUrl MUST
        // NOT emit a redundant `tab=events` query parameter. The canonical default URL
        // is `/types` exactly — assert ends-with form rather than the looser absence of
        // `tab=events`, which would let a regression emit `tab=foo` and still pass.
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/types?tab=commands", replace: true);

        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("CreateOrder"), TimeSpan.FromSeconds(5));

        // Switch back to the default events tab.
        System.Reflection.MethodInfo? method = cut.Instance.GetType()
            .GetMethod("OnTabChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method.ShouldNotBeNull(
            customMessage: "DW5 AC#4: TypeCatalog.OnTabChanged renamed/removed — test must be updated alongside the production refactor.");
        await cut.InvokeAsync(() => method!.Invoke(cut.Instance, ["events"]));
        cut.Render();

        Uri uri = new(nav.Uri);
        uri.AbsolutePath.ShouldEndWith("/types",
            customMessage: $"DW5 AC#4: canonical default URL must end with /types; got '{nav.Uri}'.");
        bool queryIsCanonical = uri.Query.Length == 0 || uri.Query == "?";
        queryIsCanonical.ShouldBeTrue(
            customMessage: $"DW5 AC#4: canonical default URL must have an empty query when events tab is active and no filters set; got '{nav.Uri}' (query='{uri.Query}').");
    }

    private void SeedSampleTypes() {
        IReadOnlyList<EventTypeInfo> events = [
            new("OrderCreated", "ordering", false, 1),
        ];
        IReadOnlyList<CommandTypeInfo> commands = [
            new("CreateOrder", "ordering", "OrderAggregate"),
        ];
        IReadOnlyList<AggregateTypeInfo> aggregates = [
            new("OrderAggregate", "ordering", 1, 1, false),
        ];

        _ = _mockApiClient.ListEventTypesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(events));
        _ = _mockApiClient.ListCommandTypesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(commands));
        _ = _mockApiClient.ListAggregateTypesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(aggregates));
    }
}
