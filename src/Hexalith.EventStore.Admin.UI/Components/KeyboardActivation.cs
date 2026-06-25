namespace Hexalith.EventStore.Admin.UI.Components;

using Microsoft.AspNetCore.Components.Web;

/// <summary>
/// Shared keyboard helpers for custom interactive elements (<c>role="button"</c> on a
/// non-native control). Centralises the activation-key predicate so every hand-rolled keydown
/// handler stays consistent (single source of truth for which keys activate a custom button).
/// </summary>
internal static class KeyboardActivation
{
    /// <summary>
    /// Returns <see langword="true"/> when the key event should activate a custom
    /// <c>role="button"</c> element, matching native button semantics (Enter or Space).
    /// <c>"Spacebar"</c> is the legacy Edge/IE value of <see cref="KeyboardEventArgs.Key"/>;
    /// it is kept for resilience even though modern browsers emit <c>" "</c>.
    /// </summary>
    /// <param name="e">The keyboard event arguments.</param>
    /// <returns><see langword="true"/> if the key is Enter or Space; otherwise <see langword="false"/>.</returns>
    public static bool IsActivationKey(this KeyboardEventArgs e)
        => e.Key is "Enter" or " " or "Spacebar";
}
