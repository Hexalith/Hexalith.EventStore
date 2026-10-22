using System.Reflection;

using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;
using Hexalith.EventStore.Admin.UI.Pages;
using Hexalith.EventStore.Admin.UI.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

// ATDD red-phase regression tests for story:
//   _bmad-output/implementation-artifacts/post-epic-deferred-dw15-admin-ui-blazor-navigation-hygiene.md
//
// AC#1, #2, #6, #7, #8, #9, #11, #12 — TypeCatalog URL updates must stop safely when
// the component has begun DisposeAsync or the Blazor Server circuit is being torn
// down. The observed CC-6 evidence was a JSDisconnectedException raised from
// NavigationManager.NavigateTo inside UpdateUrl / OnTabChanged during teardown.
//
// These bUnit tests pin the guard contract independently of the existing
// "user-left-page" and "target-equals-current" idempotency guards. They cover:
//   - UpdateUrl after _disposed = true must not call NavigateTo.
//   - Pending debounce continuations must not call NavigateTo after dispose.
//   - DashboardRefreshService.OnDataChanged signal after dispose must not navigate.
//   - /types?tab=aggregates&type=CounterAggregate selects the aggregate (Issue 12
//     fixture shape, not the seeded OrderCreated happy path).
//   - Normal active interaction still updates the URL (negative-of-negative proof
//     so the disposal fix does not silently kill the URL contract).
public class Dw15TypeCatalogDisposalSafeNavigationAtddTests : AdminUITestContext {
    private readonly AdminTypeCatalogApiClient _mockApiClient;

    public Dw15TypeCatalogDisposalSafeNavigationAtddTests() {
        _mockApiClient = Substitute.For<AdminTypeCatalogApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminTypeCatalogApiClient>.Instance);
        SeedCounterFixture();
        _ = Services.AddScoped(_ => _mockApiClient);
    }

    [Fact]
    public async Task UpdateUrl_DoesNotNavigate_AfterDisposedFlagSet() {
        // AC#1, #7, #10 — Once DisposeAsync has started, _disposed must be true and
        // UpdateUrl must short-circuit before NavigationManager.NavigateTo. We
        // exercise the guard directly: set state that would otherwise produce a
        // different target URL, then flip _disposed and assert no LocationChanged.
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/types", replace: true);

        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Type Catalog"), TimeSpan.FromSeconds(5));

        // Force a state divergence so the target URL would be different from the
        // current URL — otherwise the existing idempotency guard would short-circuit
        // for an unrelated reason and not exercise the _disposed guard.
        SetPrivateField(cut.Instance, "_activeTab", "commands");

        // Flip the _disposed guard. If the production code does not declare it,
        // this test must fail loudly with a clear assertion message that points the
        // developer at the missing guard.
        bool guardFieldExists = TryGetPrivateField(cut.Instance, "_disposed", out _);
        guardFieldExists.ShouldBeTrue(
            customMessage: "DW15 AC#1: TypeCatalog must declare a _disposed flag for UpdateUrl to short-circuit safely after disposal begins.");
        SetPrivateField(cut.Instance, "_disposed", true);

        int navigationCount = 0;
        nav.LocationChanged += (_, _) => navigationCount++;

        MethodInfo updateUrl = typeof(TypeCatalog)
            .GetMethod("UpdateUrl", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await cut.InvokeAsync(() => {
            _ = updateUrl.Invoke(cut.Instance, new object?[] { true });
        });

        navigationCount.ShouldBe(0,
            customMessage: "DW15 AC#1/#7: UpdateUrl invoked after _disposed=true must not call NavigateTo (CC-6 disposal-safe guard).");
        nav.Uri.ShouldEndWith("/types",
            customMessage: "DW15 AC#1: late UpdateUrl must not change NavigationManager.Uri after disposal begins.");
    }

    [Fact]
    public async Task UpdateUrl_DoesNotNavigate_AfterDisposeAsyncCompleted() {
        // AC#1, #7, #11 — A stray ActiveTabIdChanged firing after DisposeAsync has
        // already cancelled the debounce CTS and removed the refresh subscription
        // must still not call NavigateTo. This is the realistic end-to-end shape of
        // CC-6: FluentTabs v5 fires the callback during teardown.
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/types", replace: true);

        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Type Catalog"), TimeSpan.FromSeconds(5));

        // Force a tab divergence so the target URL would NOT equal the current URL —
        // this isolates the _disposed guard from the path/query idempotency guard.
        SetPrivateField(cut.Instance, "_activeTab", "aggregates");

        // Drive DisposeAsync just like the framework would on circuit teardown.
        await cut.InvokeAsync(async () => {
            await ((IAsyncDisposable)cut.Instance).DisposeAsync().ConfigureAwait(false);
        });

        int navigationCount = 0;
        nav.LocationChanged += (_, _) => navigationCount++;

        MethodInfo updateUrl = typeof(TypeCatalog)
            .GetMethod("UpdateUrl", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Exception? caught = null;
        await cut.InvokeAsync(() => {
            try {
                _ = updateUrl.Invoke(cut.Instance, new object?[] { true });
            }
            catch (TargetInvocationException tie) when (tie.InnerException is not null) {
                caught = tie.InnerException;
            }
            catch (Exception ex) {
                caught = ex;
            }
        });

        caught.ShouldBeNull(
            customMessage: $"DW15 AC#10: UpdateUrl after DisposeAsync must not throw; got {caught?.GetType().Name}: {caught?.Message}.");
        navigationCount.ShouldBe(0,
            customMessage: "DW15 AC#1/#2: stray FluentTabs callback after DisposeAsync must not trigger NavigateTo back to /types.");
        nav.Uri.ShouldEndWith("/types",
            customMessage: "DW15 AC#1: stray late UpdateUrl must not mutate NavigationManager.Uri after DisposeAsync.");
    }

    [Fact]
    public async Task PendingSearchDebounce_DoesNotNavigate_WhenDisposeBeforeTimerFires() {
        // AC#8, #11 — A debounced search continuation scheduled before DisposeAsync
        // must not call NavigateTo after disposal begins. The current implementation
        // cancels the debounce CTS in DisposeAsync, so Task.Delay throws
        // OperationCanceledException and the continuation never runs UpdateUrl. This
        // test pins that contract and would catch a regression where the CTS guard
        // is removed or the catch swallows non-teardown exceptions.
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/types", replace: true);

        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Type Catalog"), TimeSpan.FromSeconds(5));

        // Schedule a debounced search — production debounce delay is 300ms.
        MethodInfo onSearchChanged = typeof(TypeCatalog)
            .GetMethod("OnSearchValueChanged", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await cut.InvokeAsync(() => {
            _ = onSearchChanged.Invoke(cut.Instance, new object?[] { "Counter" });
        });

        int navigationCount = 0;
        nav.LocationChanged += (_, _) => navigationCount++;

        // Dispose well before the 300 ms debounce would fire.
        await cut.InvokeAsync(async () => {
            await ((IAsyncDisposable)cut.Instance).DisposeAsync().ConfigureAwait(false);
        });

        // Wait past the debounce window. If the pending continuation reaches
        // UpdateUrl after disposal, a LocationChanged event would fire.
        await Task.Delay(500);

        navigationCount.ShouldBe(0,
            customMessage: "DW15 AC#8/#11: pending search debounce continuation must not call NavigateTo after DisposeAsync.");
    }

    [Fact]
    public async Task SearchCallback_DoesNotThrowOrNavigate_WhenInvokedAfterDispose() {
        // AC#8, #11 — A late FluentTextField callback can arrive after DisposeAsync.
        // It must return before touching the disposed debounce CTS or scheduling a
        // new URL update.
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/types", replace: true);

        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Type Catalog"), TimeSpan.FromSeconds(5));

        MethodInfo onSearchChanged = typeof(TypeCatalog)
            .GetMethod("OnSearchValueChanged", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await cut.InvokeAsync(() => {
            _ = onSearchChanged.Invoke(cut.Instance, new object?[] { "Counter" });
        });

        await cut.InvokeAsync(async () => {
            await ((IAsyncDisposable)cut.Instance).DisposeAsync().ConfigureAwait(false);
        });

        int navigationCount = 0;
        nav.LocationChanged += (_, _) => navigationCount++;

        Exception? caught = null;
        await cut.InvokeAsync(() => {
            try {
                _ = onSearchChanged.Invoke(cut.Instance, new object?[] { "CounterReset" });
            }
            catch (TargetInvocationException tie) when (tie.InnerException is not null) {
                caught = tie.InnerException;
            }
            catch (Exception ex) {
                caught = ex;
            }
        });

        caught.ShouldBeNull(
            customMessage: $"DW15 AC#11: late search callback after DisposeAsync must not throw; got {caught?.GetType().Name}: {caught?.Message}.");
        navigationCount.ShouldBe(0,
            customMessage: "DW15 AC#8/#11: late search callback after DisposeAsync must not call NavigateTo.");
    }

    [Fact]
    public async Task InitialLoad_DoesNotSubscribeRefresh_WhenDisposedBeforeLoadCompletes() {
        // AC#11 — Blazor may dispose a component before awaited lifecycle work has
        // completed. If TypeCatalog is disposed while the initial load awaits API
        // data, the OnInitializedAsync continuation must not subscribe a disposed
        // component to DashboardRefreshService.OnDataChanged.
        TaskCompletionSource<IReadOnlyList<EventTypeInfo>> eventsSource = new();
        TaskCompletionSource<IReadOnlyList<CommandTypeInfo>> commandsSource = new();
        TaskCompletionSource<IReadOnlyList<AggregateTypeInfo>> aggregatesSource = new();

        _ = _mockApiClient.ListEventTypesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(eventsSource.Task);
        _ = _mockApiClient.ListCommandTypesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(commandsSource.Task);
        _ = _mockApiClient.ListAggregateTypesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(aggregatesSource.Task);

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/types", replace: true);

        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();

        await cut.InvokeAsync(async () => {
            await ((IAsyncDisposable)cut.Instance).DisposeAsync().ConfigureAwait(false);
        });

        eventsSource.SetResult([]);
        commandsSource.SetResult([]);
        aggregatesSource.SetResult([]);

        await Task.Delay(100);

        DashboardRefreshService refreshService = Services.GetRequiredService<DashboardRefreshService>();
        Delegate? subscribers = GetPrivateField<Delegate?>(refreshService, "OnDataChanged");
        subscribers.ShouldBeNull(
            customMessage: "DW15 AC#11: disposed TypeCatalog must not subscribe to DashboardRefreshService after initial LoadDataAsync completes.");
    }

    [Fact]
    public async Task RefreshSignal_DoesNotNavigate_AfterDispose() {
        // AC#11 — DashboardRefreshService.OnDataChanged fired after DisposeAsync
        // must not navigate. The current implementation unsubscribes in DisposeAsync,
        // so the handler is never invoked. This test pins that contract and would
        // catch a regression where the subscription is leaked.
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/types", replace: true);

        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Type Catalog"), TimeSpan.FromSeconds(5));

        // Drive DisposeAsync, then attempt to invoke the refresh callback by name.
        // OnRefreshSignal is a private method; we expect the subscription removal in
        // DisposeAsync to prevent the real RefreshService from reaching it.
        await cut.InvokeAsync(async () => {
            await ((IAsyncDisposable)cut.Instance).DisposeAsync().ConfigureAwait(false);
        });

        int navigationCount = 0;
        nav.LocationChanged += (_, _) => navigationCount++;

        // Even if the subscription was somehow leaked, calling OnRefreshSignal
        // directly post-dispose must not navigate. Production code paths in
        // OnRefreshSignal do not call UpdateUrl today, but we still pin the
        // no-navigation contract so a future change that adds a URL update inside
        // the refresh path cannot leak into disposal teardown.
        MethodInfo? refreshSignal = typeof(TypeCatalog)
            .GetMethod("OnRefreshSignal", BindingFlags.NonPublic | BindingFlags.Instance);
        if (refreshSignal is not null) {
            Exception? caught = null;
            await cut.InvokeAsync(() => {
                try {
                    _ = refreshSignal.Invoke(
                        cut.Instance,
                        new object?[] { new DashboardData(null, null) });
                }
                catch (TargetInvocationException tie) when (tie.InnerException is not null) {
                    caught = tie.InnerException;
                }
                catch (Exception ex) {
                    caught = ex;
                }
            });
            caught.ShouldBeNull(
                customMessage: $"DW15 AC#11: refresh signal after DisposeAsync must not throw; got {caught?.GetType().Name}: {caught?.Message}.");
        }

        await Task.Delay(100);

        navigationCount.ShouldBe(0,
            customMessage: "DW15 AC#11: refresh signal after DisposeAsync must not trigger NavigateTo.");
    }

    [Fact]
    public void DeepLink_TabAggregatesType_CounterAggregate_SelectsAggregate() {
        // AC#3, #9 — /types?tab=aggregates&type=CounterAggregate must land on the
        // aggregates tab AND select CounterAggregate. The manual evidence fixture
        // uses CounterAggregate, so this regression must use that exact shape.
        // A selection marker (the type name appearing outside the table row) is the
        // deterministic signal — we seed two aggregates so a non-selecting render
        // still lists both, and the selected name must appear at least twice.
        IReadOnlyList<AggregateTypeInfo> aggregates = [
            new("CounterAggregate", "samples", 3, 3, false),
            new("OrderAggregate", "ordering", 1, 1, true),
        ];
        _ = _mockApiClient.ListAggregateTypesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(aggregates));

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/types?tab=aggregates&type=CounterAggregate", replace: true);

        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("CounterAggregate"),
            TimeSpan.FromSeconds(5));

        // Active tab must be aggregates.
        FieldInfo activeTabField = typeof(TypeCatalog)
            .GetField("_activeTab", BindingFlags.NonPublic | BindingFlags.Instance)!;
        ((string?)activeTabField.GetValue(cut.Instance)).ShouldBe("aggregates",
            customMessage: "DW15 AC#3: deep link /types?tab=aggregates&type=CounterAggregate must initialize aggregates tab.");

        // Selection must be applied (not just the URL parameter parsed).
        int occurrences = System.Text.RegularExpressions.Regex
            .Matches(cut.Markup, "CounterAggregate")
            .Count;
        occurrences.ShouldBeGreaterThan(1,
            customMessage: "DW15 AC#3/#9: /types?tab=aggregates&type=CounterAggregate must select the aggregate (detail-panel marker), not just list it.");

        AggregateTypeInfo? selectedAggregate = GetPrivateField<AggregateTypeInfo?>(cut.Instance, "_selectedAggregate");
        selectedAggregate.ShouldNotBeNull(
            customMessage: "DW15 AC#3/#9: /types?tab=aggregates&type=CounterAggregate must set the aggregate selection state.");
        selectedAggregate.TypeName.ShouldBe("CounterAggregate");
    }

    [Fact]
    public void DeepLink_BareType_CounterAggregate_SelectsAggregateTab() {
        // AC#3, #9 — /types?type=CounterAggregate is an accepted canonical deep
        // link. Without an explicit tab parameter, TypeCatalog must infer the
        // aggregate tab from the loaded catalog instead of staying on events.
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/types?type=CounterAggregate", replace: true);

        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("CounterAggregate"),
            TimeSpan.FromSeconds(5));

        GetPrivateField<string>(cut.Instance, "_activeTab").ShouldBe("aggregates",
            customMessage: "DW15 AC#3: /types?type=CounterAggregate must infer the aggregates tab.");

        AggregateTypeInfo? selectedAggregate = GetPrivateField<AggregateTypeInfo?>(cut.Instance, "_selectedAggregate");
        selectedAggregate.ShouldNotBeNull(
            customMessage: "DW15 AC#3/#9: bare CounterAggregate deep link must set aggregate selection state.");
        selectedAggregate.TypeName.ShouldBe("CounterAggregate");
    }

    [Fact]
    public async Task NormalTabChange_StillUpdatesUrl_WhenComponentActive() {
        // AC#6 — Active interaction must keep working. Switching from the default
        // events tab to aggregates while the user is still on /types and not
        // disposed must update the URL. This is the negative-of-negative proof so
        // the DW15 disposal guard does not silently kill the URL contract.
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/types", replace: true);

        IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Type Catalog"), TimeSpan.FromSeconds(5));

        int navigationCount = 0;
        nav.LocationChanged += (_, _) => navigationCount++;

        MethodInfo onTabChanged = typeof(TypeCatalog)
            .GetMethod("OnTabChanged", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await cut.InvokeAsync(() => {
            _ = onTabChanged.Invoke(cut.Instance, new object?[] { "aggregates" });
        });

        cut.WaitForAssertion(
            () => navigationCount.ShouldBeGreaterThan(0),
            TimeSpan.FromSeconds(2));
        nav.Uri.ShouldContain("tab=aggregates",
            customMessage: "DW15 AC#6: active tab change must still update the URL when the component is not disposed and the user is on /types.");
    }

    private static void SetPrivateField(object target, string fieldName, object? value) {
        FieldInfo field = target.GetType()
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(target, value);
    }

    private static bool TryGetPrivateField(object target, string fieldName, out FieldInfo? field) {
        field = target.GetType()
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        return field is not null;
    }

    private static T GetPrivateField<T>(object target, string fieldName) {
        FieldInfo field = target.GetType()
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (T)field.GetValue(target)!;
    }

    private void SeedCounterFixture() {
        IReadOnlyList<EventTypeInfo> events = [
            new("CounterIncremented", "samples", false, 1),
            new("CounterDecremented", "samples", false, 1),
            new("CounterReset", "samples", false, 1),
        ];
        IReadOnlyList<CommandTypeInfo> commands = [
            new("IncrementCounter", "samples", "CounterAggregate"),
            new("DecrementCounter", "samples", "CounterAggregate"),
            new("ResetCounter", "samples", "CounterAggregate"),
        ];
        IReadOnlyList<AggregateTypeInfo> aggregates = [
            new("CounterAggregate", "samples", 3, 3, false),
        ];

        _ = _mockApiClient.ListEventTypesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(events));
        _ = _mockApiClient.ListCommandTypesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(commands));
        _ = _mockApiClient.ListAggregateTypesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(aggregates));
    }
}
