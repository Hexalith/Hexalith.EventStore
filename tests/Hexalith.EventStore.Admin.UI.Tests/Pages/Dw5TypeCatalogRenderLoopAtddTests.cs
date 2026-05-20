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

    [Fact]
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

    [Fact]
    public void TypeCatalog_UpdateUrl_DoesNotGrowRenderCount_OnRepeatedSameTabSelection() {
        // AC#3/#4 — Re-selecting the same tab MUST be idempotent at the render level.
        // OnTabChanged early-returns when the tab is already active, so calling it twice
        // exercises that guard rather than UpdateUrl. We assert RenderCount does not
        // grow on the second invocation, which is the loop-absence signal — markup
        // equality on its own would pass even if a render loop converged to the same
        // final state.
        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Type Catalog"), TimeSpan.FromSeconds(5));

        // Switch to a non-default tab so the second InvokeOnTabChanged exercises the
        // already-active early-return path (rather than the no-op default tab).
        InvokeOnTabChanged(cut, "commands");
        cut.WaitForState(() => cut.RenderCount > 0, TimeSpan.FromSeconds(5));
        int renderCountAfterFirst = cut.RenderCount;

        InvokeOnTabChanged(cut, "commands");
        int renderCountAfterSecond = cut.RenderCount;

        renderCountAfterSecond.ShouldBe(renderCountAfterFirst,
            customMessage: "DW5 AC#3/#4: re-selecting the already-active tab must not trigger a rerender (early-return guard, no oscillation).");
    }

    private static void InvokeOnTabChanged(IRenderedComponent<TypeCatalog> cut, string tabId) {
        System.Reflection.MethodInfo? method = cut.Instance.GetType()
            .GetMethod("OnTabChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        _ = method.ShouldNotBeNull(
            customMessage: "DW5 AC#3: TypeCatalog.OnTabChanged renamed/removed — test must be updated alongside the production refactor (?.Invoke would silently no-op).");
        _ = cut.InvokeAsync(() => method!.Invoke(cut.Instance, [tabId])).GetAwaiter().GetResult();
    }
}
