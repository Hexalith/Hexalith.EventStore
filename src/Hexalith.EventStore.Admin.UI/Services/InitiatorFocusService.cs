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
    /// <remarks>
    /// Focus restoration is best-effort: a JavaScript failure, a disconnected circuit, or an interop
    /// timeout never escapes into the calling cancel, validation, or denial handler.
    /// </remarks>
    public async ValueTask RestoreAsync(string? elementId)
    {
        if (string.IsNullOrWhiteSpace(elementId))
        {
            return;
        }

        try
        {
            await jsRuntime.InvokeVoidAsync("hexalithAdmin.waitForRender").ConfigureAwait(false);
            await jsRuntime.InvokeVoidAsync("hexalithAdmin.focusElementById", elementId).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or OperationCanceledException)
        {
            // The handler has already settled its state; a lost focus move must not fault it.
        }
    }
}
