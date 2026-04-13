using Microsoft.FluentUI.AspNetCore.Components;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// Stores the current Fluent UI theme mode for the active circuit.
/// </summary>
public sealed class ThemeState
{
    private ThemeMode _mode = ThemeMode.System;

    public event Action? Changed;

    public ThemeMode Mode => _mode;

    public void SetMode(ThemeMode mode)
    {
        if (_mode == mode)
        {
            return;
        }

        _mode = mode;
        Changed?.Invoke();
    }
}
