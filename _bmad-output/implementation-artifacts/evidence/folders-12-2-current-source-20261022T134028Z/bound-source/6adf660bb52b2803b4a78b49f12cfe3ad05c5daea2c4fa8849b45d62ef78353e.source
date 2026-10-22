using Microsoft.FluentUI.AspNetCore.Components;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// Stores the current Fluent UI theme mode for the active circuit.
/// </summary>
public sealed class ThemeState {
    public event Action? Changed;

    public ThemeMode Mode { get; private set; } = ThemeMode.System;

    public void SetMode(ThemeMode mode) {
        if (Mode == mode) {
            return;
        }

        Mode = mode;
        Changed?.Invoke();
    }
}
