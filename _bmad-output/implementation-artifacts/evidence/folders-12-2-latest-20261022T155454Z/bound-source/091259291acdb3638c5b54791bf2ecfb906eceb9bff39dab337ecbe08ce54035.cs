namespace Hexalith.EventStore.Client.Events;

/// <summary>Rechecks the exact registry, original token and immutable bindings around an optional addressed asynchronous fence.</summary>
internal static class EventCallbackFence
{
    /// <summary>Admits the next callable only after both local and addressed authority remain current.</summary>
    internal static async ValueTask RequireAsync(EventDomainRegistry registry,
        Func<CancellationToken, Task>? sourceFence, CancellationToken token, Action? requireBindings = null)
    {
        token.ThrowIfCancellationRequested();
        registry.RequireActive(token);
        registry.CapabilityLoss.RequireNoObservedLoss();
        requireBindings?.Invoke();
        token.ThrowIfCancellationRequested();
        if (sourceFence is not null)
        {
            await sourceFence(token).ConfigureAwait(false);
        }

        token.ThrowIfCancellationRequested();
        registry.RequireActive(token);
        registry.CapabilityLoss.RequireNoObservedLoss();
        requireBindings?.Invoke();
        token.ThrowIfCancellationRequested();
        registry.RequireActive(token);
        registry.CapabilityLoss.RequireNoObservedLoss();
    }
}
