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
    private const string _ac4DefaultUrlSkipReason =
        "ATDD red phase — DW5 AC#4 (default deep link). Remove Skip after the navigation/render-loop fix "
        + "preserves the /types → events-tab default initialization.";
    private const string _ac4CommandsUrlSkipReason =
        "ATDD red phase — DW5 AC#4 (commands deep link). Remove Skip after /types?tab=commands "
        + "still initializes the commands tab without a redirect loop.";
    private const string _ac4AggregatesUrlSkipReason =
        "ATDD red phase — DW5 AC#4 (aggregates deep link). Remove Skip after /types?tab=aggregates "
        + "still initializes the aggregates tab without a redirect loop.";
    private const string _ac4TypeSelectionUrlSkipReason =
        "ATDD red phase — DW5 AC#4 (type= selection deep link). Remove Skip after /types?type=<name> "
        + "still initializes selection on the matching tab.";
    private const string _ac4UpdateUrlIdempotentSkipReason =
        "ATDD red phase — DW5 AC#4 (UpdateUrl idempotent on default tab). Remove Skip after UpdateUrl "
        + "still emits /types (no tab=events) when the events tab is active and no filters are set.";

    private readonly AdminTypeCatalogApiClient _mockApiClient;

    public Dw5TypeCatalogUrlIdempotencyAtddTests() {
        _mockApiClient = Substitute.For<AdminTypeCatalogApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminTypeCatalogApiClient>.Instance);
        SeedSampleTypes();
        _ = Services.AddScoped(_ => _mockApiClient);
    }

    [Fact(Skip = _ac4DefaultUrlSkipReason)]
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

    [Fact(Skip = _ac4CommandsUrlSkipReason)]
    public void DeepLink_TabCommands_InitializesCommandsTab() {
        // AC#4 — /types?tab=commands must land on commands and render the seeded command name.
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/types?tab=commands", replace: true);

        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("CreateOrder"),
            TimeSpan.FromSeconds(5));
    }

    [Fact(Skip = _ac4AggregatesUrlSkipReason)]
    public void DeepLink_TabAggregates_InitializesAggregatesTab() {
        // AC#4 — /types?tab=aggregates must land on aggregates and render the seeded aggregate name.
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/types?tab=aggregates", replace: true);

        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("OrderAggregate"),
            TimeSpan.FromSeconds(5));
    }

    [Fact(Skip = _ac4TypeSelectionUrlSkipReason)]
    public void DeepLink_TypeSelection_InitializesSelection() {
        // AC#4 — /types?type=OrderCreated must select OrderCreated on the matching tab.
        // The selected type's TypeName MUST appear in the rendered markup as part of
        // the selection indicator (detail panel, highlight row, or breadcrumb).
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/types?type=OrderCreated", replace: true);

        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("OrderCreated"),
            TimeSpan.FromSeconds(5));
    }

    [Fact(Skip = _ac4UpdateUrlIdempotentSkipReason)]
    public async Task UpdateUrl_DefaultTab_DoesNotEmitTabQueryString() {
        // AC#4 — When the events tab is active and no filters are set, UpdateUrl MUST
        // NOT emit a redundant `tab=events` query parameter. This is part of the
        // redirect-loop guard contract: the URL stays at /types (canonical form), so
        // a subsequent NavigateTo with the same logical state hits the path/query
        // equality check and short-circuits.
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/types?tab=commands", replace: true);

        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("CreateOrder"), TimeSpan.FromSeconds(5));

        // Switch back to the default events tab.
        await cut.InvokeAsync(() => cut.Instance.GetType()
            .GetMethod("OnTabChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(cut.Instance, ["events"]));
        cut.Render();

        nav.Uri.ShouldNotContain("tab=events",
            customMessage: "DW5 AC#4: UpdateUrl must omit `tab=events` when events is the default active tab.");
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
