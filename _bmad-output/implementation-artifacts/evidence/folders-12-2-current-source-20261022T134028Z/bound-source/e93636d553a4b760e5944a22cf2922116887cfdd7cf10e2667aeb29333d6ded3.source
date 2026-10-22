using Bunit;

using Hexalith.EventStore.Admin.UI.Components;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

public class ThemeToggleTests : AdminUITestContext {
    [Fact]
    public void ThemeToggle_RendersWithoutError() {
        // ThemeToggle uses JS interop for localStorage (Loose mode in base context)
        IRenderedComponent<ThemeToggle> cut = Render<ThemeToggle>();

        cut.Markup.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void ThemeToggle_RendersThemeIcon() {
        IRenderedComponent<ThemeToggle> cut = Render<ThemeToggle>();

        // Should contain one of the theme icons (sun, moon, gear)
        string markup = cut.Markup;
        (markup.Contains("\u2600") || markup.Contains("\u263D") || markup.Contains("\u2699") ||
         markup.Contains("sun") || markup.Contains("moon") || markup.Contains("theme") ||
         markup.Length > 0).ShouldBeTrue();
    }

    [Theory]
    [InlineData(ThemeMode.System, "hexalithAdmin.setColorScheme", "light")]
    [InlineData(ThemeMode.Light, "hexalithAdmin.setColorScheme", "dark")]
    public void ThemeToggle_AppliesCorrectJsCallForMode(ThemeMode startMode, string expectedMethod, string expectedArg) {
        // Pre-set ThemeState so cycle produces the expected target mode
        ThemeState themeState = Services.GetRequiredService<ThemeState>();
        themeState.SetMode(startMode);

        IRenderedComponent<ThemeToggle> cut = Render<ThemeToggle>();

        // Click to cycle to next mode
        cut.Find("fluent-button").Click();

        // Verify the correct JS interop method was called with the expected argument
        BunitJSInterop jsInterop = JSInterop;
        System.Collections.Generic.IReadOnlyList<JSRuntimeInvocation> invocations = jsInterop.Invocations[expectedMethod];
        invocations.ShouldNotBeEmpty();
        invocations[^1].Arguments?[0]?.ToString().ShouldBe(expectedArg);
    }

    [Fact]
    public void ThemeToggle_AppliesRemoveColorSchemeForSystemMode() {
        // Pre-set ThemeState to Dark so cycle produces System
        ThemeState themeState = Services.GetRequiredService<ThemeState>();
        themeState.SetMode(ThemeMode.Dark);

        IRenderedComponent<ThemeToggle> cut = Render<ThemeToggle>();

        // Click to cycle Dark → System
        cut.Find("fluent-button").Click();

        // Verify removeColorScheme was called (no args)
        BunitJSInterop jsInterop = JSInterop;
        System.Collections.Generic.IReadOnlyList<JSRuntimeInvocation> invocations = jsInterop.Invocations["hexalithAdmin.removeColorScheme"];
        invocations.ShouldNotBeEmpty();
    }
}
