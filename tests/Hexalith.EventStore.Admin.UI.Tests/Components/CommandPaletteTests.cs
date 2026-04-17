using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

/// <summary>
/// Focused regression tests for command palette open, fuzzy filtering, and navigation behavior.
/// </summary>
public class CommandPaletteTests : AdminUITestContext {
    private const System.Reflection.BindingFlags PrivateInstance =
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    [Fact]
    public async Task CommandPalette_Open_ShowsCatalogAndRequestsFocus() {
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Components.CommandPalette> cut = Render<Hexalith.EventStore.Admin.UI.Components.CommandPalette>();

        await cut.InvokeAsync(cut.Instance.OpenAsync);

        // v5 FluentDialog markup does not include inline children until opened via JS;
        // assert via the component's filtered-results state instead.
        cut.WaitForAssertion(() => {
            IReadOnlyList<Hexalith.EventStore.Admin.UI.Components.CommandPaletteItem> results = GetFilteredResults(cut.Instance);
            results.Select(i => i.Label).ShouldContain("Health Dashboard");
            results.Select(i => i.Label).ShouldContain("Manage Tenants");
        });

        // v5 FluentDialog.ShowAsync never completes under bUnit's loose JSInterop so _isOpen stays
        // false and OnAfterRenderAsync's JS focus call is short-circuited. Verify the equivalent
        // intent: OpenAsync requested focus by setting the internal flag. If a future regression
        // removes the flag or the JS call target name, this still catches the former.
        System.Reflection.FieldInfo focusField = GetRequiredPrivateField("_focusSearchRequested");
        object? focusValue = focusField.GetValue(cut.Instance);
        _ = focusValue.ShouldNotBeNull();
        ((bool)focusValue).ShouldBeTrue();
    }

    private static IReadOnlyList<Hexalith.EventStore.Admin.UI.Components.CommandPaletteItem> GetFilteredResults(
        Hexalith.EventStore.Admin.UI.Components.CommandPalette palette) {
        System.Reflection.FieldInfo field = GetRequiredPrivateField("_filteredResults");
        object? value = field.GetValue(palette);
        _ = value.ShouldNotBeNull();
        return (IReadOnlyList<Hexalith.EventStore.Admin.UI.Components.CommandPaletteItem>)value;
    }

    private static System.Reflection.FieldInfo GetRequiredPrivateField(string fieldName)
        => typeof(Hexalith.EventStore.Admin.UI.Components.CommandPalette)
            .GetField(fieldName, PrivateInstance)
            ?? throw new InvalidOperationException($"Private field '{fieldName}' not found on CommandPalette; test assumptions need refresh.");

    private static System.Reflection.MethodInfo GetRequiredPrivateMethod(string methodName)
        => typeof(Hexalith.EventStore.Admin.UI.Components.CommandPalette)
            .GetMethod(methodName, PrivateInstance)
            ?? throw new InvalidOperationException($"Private method '{methodName}' not found on CommandPalette; test assumptions need refresh.");

    [Fact]
    public void CommandPaletteCatalog_Filter_FindsFuzzyTenantMatch() {
        IReadOnlyList<Hexalith.EventStore.Admin.UI.Components.CommandPaletteItem> results = Hexalith.EventStore.Admin.UI.Components.CommandPaletteCatalog.Filter("mng tnts");

        results.ShouldNotBeEmpty();
        results.First().Label.ShouldBe("Manage Tenants");
    }

    [Fact]
    public async Task CommandPalette_ShowAsyncHideAsync_Lifecycle() {
        // AC 32b: Catches `async void` mistakes and verifies the v5 ShowAsync/HideAsync lifecycle.
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Components.CommandPalette> cut = Render<Hexalith.EventStore.Admin.UI.Components.CommandPalette>();

        // Open returns Task — `await` proves the API is async (would not compile against void or `async void`).
        await cut.InvokeAsync(cut.Instance.OpenAsync);

        // v5 FluentDialog markup does not include inline children until opened via JS.
        cut.WaitForAssertion(() =>
            GetFilteredResults(cut.Instance).Select(i => i.Label).ShouldContain("Health Dashboard"));

        // Close returns Task — `await` proves the API is async.
        await cut.InvokeAsync(cut.Instance.CloseAsync);
    }

    [Fact]
    public void CommandPalette_FluentDialog_CarriesAriaLabel() {
        // Regression signal only — does NOT prove runtime ARIA correctness (FluentDialog v5 may
        // render the attribute into shadow DOM which bUnit does not traverse). Task 5.6 AT pass
        // is the real verifier. This catches the case where the `aria-label="Command palette"`
        // attribute is accidentally removed from CommandPalette.razor source.
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Components.CommandPalette> cut = Render<Hexalith.EventStore.Admin.UI.Components.CommandPalette>();

        cut.Markup.ShouldContain("aria-label=\"Command palette\"");
    }

    [Fact]
    public async Task CommandPalette_OpenAsync_DoesNotEarlyReturnWhenIsOpenStuckTrue() {
        // Regression test for Story 21-13 Bug #1 (Ctrl+K re-open).
        // If FluentDialog v5 misses emitting DialogState.Closed (Escape-triggered close),
        // `_isOpen` can stay true while state has already moved to Closing/Closed.
        // The fix preserves a recovery path for that stale-open state.
        // This test simulates that stranded state and verifies OpenAsync still progresses
        // past the guard — e.g., it re-populates _filteredResults and requests focus.
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Components.CommandPalette> cut = Render<Hexalith.EventStore.Admin.UI.Components.CommandPalette>();

        // Simulate the stuck-true state that v5 Escape-close can leave behind.
        System.Reflection.FieldInfo isOpenField = GetRequiredPrivateField("_isOpen");
        System.Reflection.FieldInfo dialogStateField = GetRequiredPrivateField("_dialogState");
        System.Reflection.FieldInfo focusField = GetRequiredPrivateField("_focusSearchRequested");
        isOpenField.SetValue(cut.Instance, true);
        dialogStateField.SetValue(cut.Instance, Microsoft.FluentUI.AspNetCore.Components.DialogState.Closed);
        focusField.SetValue(cut.Instance, false);

        // Clear _filteredResults so we can see if OpenAsync re-populates them (proves it didn't early-return).
        System.Reflection.FieldInfo resultsField = GetRequiredPrivateField("_filteredResults");
        resultsField.SetValue(cut.Instance, new List<Hexalith.EventStore.Admin.UI.Components.CommandPaletteItem>());

        await cut.InvokeAsync(cut.Instance.OpenAsync);

        // If the guard blocked the call, _filteredResults would still be empty and focus would not be requested.
        GetFilteredResults(cut.Instance).ShouldNotBeEmpty();
        ((bool)focusField.GetValue(cut.Instance)!).ShouldBeTrue();
    }

    [Fact]
    public async Task CommandPalette_OpenAsync_NoOpsWhenDialogIsActuallyOpen() {
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Components.CommandPalette> cut = Render<Hexalith.EventStore.Admin.UI.Components.CommandPalette>();

        System.Reflection.FieldInfo isOpenField = GetRequiredPrivateField("_isOpen");
        System.Reflection.FieldInfo dialogStateField = GetRequiredPrivateField("_dialogState");
        System.Reflection.FieldInfo queryField = GetRequiredPrivateField("_searchQuery");
        System.Reflection.FieldInfo focusField = GetRequiredPrivateField("_focusSearchRequested");
        System.Reflection.FieldInfo resultsField = GetRequiredPrivateField("_filteredResults");

        List<Hexalith.EventStore.Admin.UI.Components.CommandPaletteItem> seededResults =
        [
            new("Pages", "Keep Item", "/keep"),
        ];

        isOpenField.SetValue(cut.Instance, true);
        dialogStateField.SetValue(cut.Instance, Microsoft.FluentUI.AspNetCore.Components.DialogState.Open);
        queryField.SetValue(cut.Instance, "keep-me");
        focusField.SetValue(cut.Instance, false);
        resultsField.SetValue(cut.Instance, seededResults);

        await cut.InvokeAsync(cut.Instance.OpenAsync);

        ((string?)queryField.GetValue(cut.Instance)).ShouldBe("keep-me");
        ((bool)focusField.GetValue(cut.Instance)!).ShouldBeFalse();

        IReadOnlyList<Hexalith.EventStore.Admin.UI.Components.CommandPaletteItem> results = GetFilteredResults(cut.Instance);
        results.Count.ShouldBe(1);
        results[0].Label.ShouldBe("Keep Item");
    }

    [Fact]
    public async Task CommandPalette_ClickingResult_NavigatesToSelectedRoute() {
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Components.CommandPalette> cut = Render<Hexalith.EventStore.Admin.UI.Components.CommandPalette>();
        NavigationManager navigationManager = Services.GetRequiredService<NavigationManager>();

        await cut.InvokeAsync(cut.Instance.OpenAsync);

        // v5 FluentDialog does not render ChildContent to markup until actually opened via JS
        // (which bUnit's loose JSInterop cannot drive), so the result-list <fluent-button>s are
        // never reachable through FindAll. Invoke the private NavigateToAsync directly — this still
        // validates that the palette's click handler invokes NavigationManager with the result's Href.
        Hexalith.EventStore.Admin.UI.Components.CommandPaletteItem commandsItem = GetFilteredResults(cut.Instance)
            .First(i => i.Label.Equals("Commands", StringComparison.Ordinal));
        System.Reflection.MethodInfo navigateMethod = GetRequiredPrivateMethod("NavigateToAsync");
        object? invokeResult = navigateMethod.Invoke(cut.Instance, new object[] { commandsItem.Href });
        _ = invokeResult.ShouldNotBeNull();
        _ = invokeResult.ShouldBeAssignableTo<Task>();
        await cut.InvokeAsync(() => (Task)invokeResult);

        navigationManager.Uri.ShouldEndWith("/commands");
    }
}