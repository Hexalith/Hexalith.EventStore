using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;
using Hexalith.EventStore.Admin.UI.Pages;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

// ATDD red-phase scaffolds for story:
//   _bmad-output/implementation-artifacts/post-epic-deferred-dw5-admin-ui-runtime-follow-ups.md
//
// AC#3 — TypeCatalog render-loop hypotheses must be tested before broad rewrites.
//        The deferred-work note lists likely culprits: inline `_filteredEvents.AsQueryable()`
//        on FluentDataGrid Items, DashboardRefreshService.OnDataChanged subscription,
//        ViewportService.IsWideViewport flips, FluentTabs URL synchronization re-firing
//        ActiveTabIdChanged after a replace-navigation. The story forbids broad rewrites
//        unless evidence pins one of these paths to the navigation block.
//
// These scaffolds pin the deterministic guard contract: rapid tab toggles MUST NOT throw,
// MUST NOT cycle through UpdateUrl indefinitely, and MUST converge to the requested tab
// within bounded render passes. Browser-level reproduction of the actual navigation block
// (URL + visible page transition) lives in the E2E scaffold.
public class Dw5TypeCatalogRenderLoopAtddTests : AdminUITestContext {
    private const string _ac3HypothesisGuardSkipReason =
        "ATDD red phase — DW5 AC#3 (render-loop hypothesis guard). Remove Skip after the dev confirms via "
        + "browser evidence which suspect path (AsQueryable items, DashboardRefreshService subscription, "
        + "Viewport flips, or FluentTabs URL sync) caused the navigation block — and that the targeted fix "
        + "still permits rapid tab toggles without throwing or cycling.";
    private const string _ac3NoRedirectLoopSkipReason =
        "ATDD red phase — DW5 AC#3/#4 (no redirect loop). Remove Skip after UpdateUrl proves bounded NavigateTo "
        + "calls under repeated tab toggles (no infinite recursion via FluentTabs ActiveTabIdChanged refire).";

    private readonly AdminTypeCatalogApiClient _mockApiClient;

    public Dw5TypeCatalogRenderLoopAtddTests() {
        _mockApiClient = Substitute.For<AdminTypeCatalogApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminTypeCatalogApiClient>.Instance);
        _ = _mockApiClient.ListEventTypesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<EventTypeInfo>>([]));
        _ = _mockApiClient.ListCommandTypesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CommandTypeInfo>>([]));
        _ = _mockApiClient.ListAggregateTypesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AggregateTypeInfo>>([]));
        _ = Services.AddScoped(_ => _mockApiClient);
    }

    [Fact(Skip = _ac3HypothesisGuardSkipReason)]
    public void TypeCatalog_RapidTabToggles_DoNotThrow() {
        // AC#3 — Repeated programmatic tab toggles MUST NOT throw. This catches a
        // common render-loop signal: the OnTabChanged handler reschedules itself or
        // rerenders synchronously and trips a render-cycle guard. If this test throws,
        // a targeted fix (not a broad rewrite) is justified.
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Type Catalog"), TimeSpan.FromSeconds(5));

        // Toggle through all three tabs three times.
        Should.NotThrow(() => {
            for (int round = 0; round < 3; round++) {
                InvokeOnTabChanged(cut, "events");
                InvokeOnTabChanged(cut, "commands");
                InvokeOnTabChanged(cut, "aggregates");
            }
        });
    }

    [Fact(Skip = _ac3NoRedirectLoopSkipReason)]
    public void TypeCatalog_UpdateUrl_StableAfterMultipleSameTabSelections() {
        // AC#3/#4 — Selecting the same tab twice MUST NOT cycle through UpdateUrl
        // indefinitely. The current UpdateUrl implementation guards against this with
        // a path/query equality check. This scaffold pins that guarantee so a future
        // refactor cannot accidentally reintroduce the redirect loop.
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Type Catalog"), TimeSpan.FromSeconds(5));

        InvokeOnTabChanged(cut, "commands");
        cut.Render();
        string markupAfterFirst = cut.Markup;

        // Re-select the same tab. UpdateUrl should detect URL idempotency and skip
        // NavigateTo; the rendered markup MUST remain stable across the second
        // invocation rather than oscillating.
        InvokeOnTabChanged(cut, "commands");
        cut.Render();
        string markupAfterSecond = cut.Markup;

        markupAfterSecond.ShouldBe(markupAfterFirst,
            customMessage: "DW5 AC#3/#4: re-selecting the same tab must not change rendered markup (UpdateUrl idempotency).");
    }

    private static void InvokeOnTabChanged(IRenderedComponent<TypeCatalog> cut, string tabId) {
        _ = cut.InvokeAsync(() => cut.Instance.GetType()
            .GetMethod("OnTabChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(cut.Instance, [tabId]));
    }
}
