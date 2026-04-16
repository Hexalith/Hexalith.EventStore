using Microsoft.FluentUI.AspNetCore.Components;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

public class ThemeStateTests {
    [Fact]
    public void DefaultMode_IsSystem() {
        ThemeState sut = new();

        sut.Mode.ShouldBe(ThemeMode.System);
    }

    [Fact]
    public void SetMode_UpdatesMode() {
        ThemeState sut = new();

        sut.SetMode(ThemeMode.Dark);

        sut.Mode.ShouldBe(ThemeMode.Dark);
    }

    [Fact]
    public void SetMode_FiresChangedEvent() {
        ThemeState sut = new();
        bool fired = false;
        sut.Changed += () => fired = true;

        sut.SetMode(ThemeMode.Light);

        fired.ShouldBeTrue();
    }

    [Fact]
    public void SetMode_DoesNotFireChanged_WhenModeIsSame() {
        ThemeState sut = new();
        bool fired = false;
        sut.Changed += () => fired = true;

        sut.SetMode(ThemeMode.System); // same as default

        fired.ShouldBeFalse();
    }
}
