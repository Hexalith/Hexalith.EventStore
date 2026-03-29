using Microsoft.FluentUI.AspNetCore.Components;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

public class ThemeStateTests
{
    [Fact]
    public void DefaultMode_IsSystem()
    {
        ThemeState sut = new();

        sut.Mode.ShouldBe(DesignThemeModes.System);
    }

    [Fact]
    public void SetMode_UpdatesMode()
    {
        ThemeState sut = new();

        sut.SetMode(DesignThemeModes.Dark);

        sut.Mode.ShouldBe(DesignThemeModes.Dark);
    }

    [Fact]
    public void SetMode_FiresChangedEvent()
    {
        ThemeState sut = new();
        bool fired = false;
        sut.Changed += () => fired = true;

        sut.SetMode(DesignThemeModes.Light);

        fired.ShouldBeTrue();
    }

    [Fact]
    public void SetMode_DoesNotFireChanged_WhenModeIsSame()
    {
        ThemeState sut = new();
        bool fired = false;
        sut.Changed += () => fired = true;

        sut.SetMode(DesignThemeModes.System); // same as default

        fired.ShouldBeFalse();
    }
}
