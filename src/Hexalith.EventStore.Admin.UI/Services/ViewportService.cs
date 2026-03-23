using Microsoft.JSInterop;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// Service for detecting viewport width via JS interop with matchMedia.
/// Handles JSDisconnectedException gracefully during Blazor Server prerender.
/// </summary>
public sealed class ViewportService(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private const int WideBreakpoint = 1280;
    private DotNetObjectReference<ViewportService>? _dotNetRef;
    private bool _isWideViewport = true; // Default to wide for prerender safety
    private bool _initialized;
    private Action? _onViewportChanged;

    /// <summary>
    /// Gets whether the viewport is at or above the wide breakpoint (1280px).
    /// </summary>
    public bool IsWideViewport => _isWideViewport;

    /// <summary>
    /// Event raised when viewport crosses the wide breakpoint.
    /// </summary>
    public event Action? OnViewportChanged
    {
        add => _onViewportChanged += value;
        remove => _onViewportChanged -= value;
    }

    /// <summary>
    /// Initializes viewport detection via JS interop.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        try
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            int width = await jsRuntime
                .InvokeAsync<int>("hexalithAdmin.getViewportWidth")
                .ConfigureAwait(false);
            _isWideViewport = width >= WideBreakpoint;

            await jsRuntime.InvokeVoidAsync(
                "hexalithAdmin.registerViewportListener",
                _dotNetRef,
                WideBreakpoint).ConfigureAwait(false);

            _initialized = true;
        }
        catch (JSDisconnectedException)
        {
            // Blazor Server prerender — default to wide
            _isWideViewport = true;
        }
        catch (InvalidOperationException)
        {
            // JS interop not available yet — default to wide
            _isWideViewport = true;
        }
    }

    /// <summary>
    /// Called from JS when viewport crosses the breakpoint.
    /// </summary>
    [JSInvokable]
    public void OnViewportWidthChanged(bool isWide)
    {
        _isWideViewport = isWide;
        _onViewportChanged?.Invoke();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_dotNetRef is not null)
        {
            try
            {
                await jsRuntime.InvokeVoidAsync("hexalithAdmin.unregisterViewportListener")
                    .ConfigureAwait(false);
            }
            catch (JSDisconnectedException)
            {
                // Already disconnected
            }
            catch (ObjectDisposedException)
            {
                // Already disposed
            }

            _dotNetRef.Dispose();
            _dotNetRef = null;
        }
    }
}
