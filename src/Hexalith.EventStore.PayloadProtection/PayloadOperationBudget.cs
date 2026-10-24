// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 10.6 and 15; amendment sections 2-4.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Shares one monotonic read deadline through nested catalog, index and backend calls without shortening mandatory drain waits.</summary>
internal sealed class PayloadOperationBudget : IDisposable
{
    private static readonly AsyncLocal<PayloadOperationBudget?> Current = new();
    private readonly PayloadOperationBudget? _previous;
    private readonly TimeProvider _clock;
    private readonly long _started;
    private readonly TimeSpan _duration;
    private readonly CancellationTokenSource _deadline;
    private readonly CancellationTokenSource _linked;

    /// <summary>Starts a bounded phase, inheriting its enclosing deadline when present.</summary>
    internal PayloadOperationBudget(TimeProvider clock, CancellationToken token, TimeSpan? duration = null)
    {
        _previous = Current.Value;
        _clock = clock; _started = clock.GetTimestamp(); _duration = duration ?? TimeSpan.FromSeconds(10);
        _deadline = new(_duration, clock);
        _linked = _previous is null ? CancellationTokenSource.CreateLinkedTokenSource(token, _deadline.Token)
            : CancellationTokenSource.CreateLinkedTokenSource(token, _deadline.Token, _previous.Token);
        Current.Value = this;
    }
    /// <summary>Gets the cancellation token shared by nested I/O.</summary>
    internal CancellationToken Token => _linked.Token;
    /// <summary>Reads within the enclosing phase, observing abandoned faults and rejecting late values.</summary>
    internal static async ValueTask<PayloadProtectionStateValue<T>?> ReadAsync<T>(IPayloadProtectionStateStore store,
        string key, CancellationToken token = default) where T : class
    {
        RequireCurrent();
        Task<PayloadProtectionStateValue<T>?> pending = store.ReadAsync<T>(key, token).AsTask();
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, Current.Value?.Token ?? token);
            PayloadProtectionStateValue<T>? value = await pending.WaitAsync(linked.Token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested(); RequireCurrent();
            return value;
        }
        catch (OperationCanceledException)
        {
            _ = ObserveAsync(pending);
            token.ThrowIfCancellationRequested();
            throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderUnavailable);
        }
    }
    /// <summary>Rejects a result returned after any enclosing monotonic deadline, even when a dependency ignored cancellation.</summary>
    internal static void RequireCurrent()
    {
        for (PayloadOperationBudget? budget = Current.Value; budget is not null; budget = budget._previous)
        {
            if (budget._deadline.IsCancellationRequested || budget._clock.GetElapsedTime(budget._started) >= budget._duration)
            { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderUnavailable); }
        }
    }
    /// <summary>Restores the enclosing phase and releases timers.</summary>
    public void Dispose()
    {
        Current.Value = _previous;
        _linked.Dispose(); _deadline.Dispose();
    }
    private static async Task ObserveAsync<T>(Task<T> task)
    {
        try { _ = await task.ConfigureAwait(false); }
        catch { /* Never expose private state-store exceptions from an abandoned read. */ }
    }
}
