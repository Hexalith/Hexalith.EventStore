using Microsoft.FluentUI.AspNetCore.Components;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// Stores the current Fluent UI theme mode for the active circuit.
/// </summary>
public sealed class ThemeState {
    private DesignThemeModes _mode = DesignThemeModes.System;

    public event Action? Changed;

    public DesignThemeModes Mode => _mode;

    public void SetMode(DesignThemeModes mode) {
        if (_mode == mode) {
            return;
        }

        _mode = mode;
        Changed?.Invoke();
    }
}