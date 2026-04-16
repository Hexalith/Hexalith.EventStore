@using Hexalith.EventStore.Admin.UI.Services
@implements IDisposable
@inject IJSRuntime JSRuntime
@inject IThemeService ThemeService
@inject ThemeState ThemeState

<FluentButton Appearance="ButtonAppearance.Subtle"
              Title="@_tooltip"
              aria-label="@_tooltip"
              OnClick="@CycleTheme">
    @_label
</FluentButton>

@code {
    private const string ThemeStorageKey = "hexalith-admin-theme";

    private ThemeMode _currentMode => ThemeState.Mode;

    private string _tooltip => _currentMode switch
    {
        ThemeMode.Light => "Switch to dark theme",
        ThemeMode.Dark => "Switch to system theme",
        _ => "Switch to light theme",
    };

    private string _label => _currentMode switch
    {
        ThemeMode.Light => "\u2600",
        ThemeMode.Dark => "\u263D",
        _ => "\u2699",
    };

    protected override void OnInitialized()
    {
        ThemeState.Changed += OnThemeChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            string? savedTheme = await JSRuntime.InvokeAsync<string?>("hexalithAdmin.getLocalStorage", ThemeStorageKey).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(savedTheme) && Enum.TryParse<ThemeMode>(savedTheme, out ThemeMode mode))
            {
                ThemeState.SetMode(mode);
                await ApplyColorSchemeAsync(mode).ConfigureAwait(false);
            }
        }
    }

    private void OnThemeChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    private async Task CycleTheme()
    {
        ThemeMode nextMode = _currentMode switch
        {
            ThemeMode.System => ThemeMode.Light,
            ThemeMode.Light => ThemeMode.Dark,
            ThemeMode.Dark => ThemeMode.System,
            _ => ThemeMode.System,
        };

        ThemeState.SetMode(nextMode);
        await ApplyColorSchemeAsync(nextMode).ConfigureAwait(false);
        await JSRuntime.InvokeVoidAsync("hexalithAdmin.setLocalStorage", ThemeStorageKey, nextMode.ToString()).ConfigureAwait(false);
    }

    private async Task ApplyColorSchemeAsync(ThemeMode mode)
    {
        // Set project-owned CSS tokens (--hexalith-status-*, --hexalith-brand) via data-theme attribute
        // and browser UA styles (scrollbars, form controls) via color-scheme property.
        // Called FIRST because it's a synchronous DOM update — project tokens are ready
        // before FluentUI's async token recalculation completes.
        switch (mode)
        {
            case ThemeMode.Light:
                await JSRuntime.InvokeVoidAsync("hexalithAdmin.setColorScheme", "light").ConfigureAwait(false);
                break;
            case ThemeMode.Dark:
                await JSRuntime.InvokeVoidAsync("hexalithAdmin.setColorScheme", "dark").ConfigureAwait(false);
                break;
            default:
                await JSRuntime.InvokeVoidAsync("hexalithAdmin.removeColorScheme").ConfigureAwait(false);
                break;
        }

        // Set FluentUI v5 theme tokens (--colorNeutralBackground1, etc.) via IThemeService
        await ThemeService.SetThemeAsync(mode).ConfigureAwait(false);
    }

    public void Dispose()
    {
        ThemeState.Changed -= OnThemeChanged;
    }
}
