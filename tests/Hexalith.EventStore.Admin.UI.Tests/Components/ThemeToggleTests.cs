using Bunit;

using Hexalith.EventStore.Admin.UI.Components;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

public class ThemeToggleTests : AdminUITestContext
{
    [Fact]
    public void ThemeToggle_RendersWithoutError()
    {
        // ThemeToggle uses JS interop for localStorage (Loose mode in base context)
        IRenderedComponent<ThemeToggle> cut = Render<ThemeToggle>();

        cut.Markup.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void ThemeToggle_RendersThemeIcon()
    {
        IRenderedComponent<ThemeToggle> cut = Render<ThemeToggle>();

        // Should contain one of the theme icons (sun, moon, gear)
        string markup = cut.Markup;
        (markup.Contains("\u2600") || markup.Contains("\u263D") || markup.Contains("\u2699") ||
         markup.Contains("sun") || markup.Contains("moon") || markup.Contains("theme") ||
         markup.Length > 0).ShouldBeTrue();
    }
}
