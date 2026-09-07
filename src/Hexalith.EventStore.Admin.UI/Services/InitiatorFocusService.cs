using Microsoft.JSInterop;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// Restores keyboard focus to the exact control that initiated an Admin interaction.
/// </summary>
public sealed class InitiatorFocusService(IJSRuntime jsRuntime)
{
    /// <summary>
    /// Restores focus to an element identified by its stable DOM identifier.
    /// </summary>
    /// <param name="elementId">The initiating element identifier, or <see langword="null"/> when none was captured.</param>
    /// <returns>A task representing the focus operation.</returns>
    public async ValueTask RestoreAsync(string? elementId)
    {
        if (string.IsNullOrWhiteSpace(elementId))
        {
            return;
        }

        await jsRuntime.InvokeVoidAsync("hexalithAdmin.focusElementById", elementId).ConfigureAwait(false);
    }
}
