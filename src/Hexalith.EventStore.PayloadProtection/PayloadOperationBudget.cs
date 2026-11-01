// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 10.6 and 15; amendment sections 2-4; approved review correction section 8.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Shares one monotonic read deadline through nested catalog, index and backend calls without shortening mandatory drain waits.</summary>
internal sealed class PayloadOperationBudget : IDisposable
{
    private static readonly AsyncLocal<PayloadOperationBudget?> Current = new();
    private readonly PayloadOperationBudget? _previous;
    private readonly PayloadOperationBudget? _parent;
    private readonly TimeProvider _clock;
    private readonly long _started;
    private readonly TimeSpan _duration;
    private readonly CancellationTokenSource _deadline;
    private readonly CancellationTokenSource _linked;
    private long _readBytes;
    private int _readOperations;

    /// <summary>Starts a bounded phase, inheriting its enclosing deadline unless explicitly isolated for failure cleanup.</summary>
    internal PayloadOperationBudget(TimeProvider clock, CancellationToken token, TimeSpan? duration = null, bool independent = false)
    {
        _previous = Current.Value;
        _parent = independent ? null : _previous;
        _clock = clock; _started = clock.GetTimestamp(); _duration = duration ?? TimeSpan.FromSeconds(10);
        _deadline = new(_duration, clock);
        _linked = _parent is null ? CancellationTokenSource.CreateLinkedTokenSource(token, _deadline.Token)
            : CancellationTokenSource.CreateLinkedTokenSource(token, _deadline.Token, _parent.Token);
        Current.Value = this;
    }
    /// <summary>Gets the cancellation token shared by nested I/O.</summary>
    internal CancellationToken Token => _linked.Token;
    /// <summary>Reads within the enclosing phase, observing abandoned faults and rejecting late values.</summary>
    internal static async ValueTask<PayloadProtectionStateValue<T>?> ReadAsync<T>(IPayloadProtectionStateStore store,
        string key, CancellationToken token = default) where T : class
    {
        RequireCurrent();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, Current.Value?.Token ?? token);
        Task<PayloadProtectionStateValue<T>?> pending = store.ReadAsync<T>(key, linked.Token).AsTask();
        try
        {
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
    /// <summary>Bounds trusted evidence callbacks that may ignore cancellation and observes abandoned completion.</summary>
    internal static async ValueTask<T> AwaitAsync<T>(Func<CancellationToken, ValueTask<T>> request, CancellationToken token, bool requireCurrentResult = true)
    {
        RequireCurrent();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, Current.Value?.Token ?? token);
        Task<T> pending = request(linked.Token).AsTask();
        try
        {
            T value = await pending.WaitAsync(linked.Token).ConfigureAwait(false);
            if (requireCurrentResult) { token.ThrowIfCancellationRequested(); RequireCurrent(); }
            return value;
        }
        catch (OperationCanceledException)
        {
            _ = ObserveAsync(pending);
            token.ThrowIfCancellationRequested();
            throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderUnavailable);
        }
    }
    /// <summary>Charges cumulative qualified transport reads to every enclosing phase.</summary>
    internal static void ChargeRead(int bytes, bool operation = false)
    {
        for (PayloadOperationBudget? budget = Current.Value; budget is not null; budget = budget._parent)
        {
            if (Interlocked.Add(ref budget._readBytes, bytes) > PayloadProtectionOperationalLimits.ReadBytes
                || (operation ? Interlocked.Increment(ref budget._readOperations) : budget._readOperations) > PayloadProtectionOperationalLimits.ReadOperations)
            { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderUnavailable, isAdmissionLimit: true); }
        }
    }
    /// <summary>Checks that an index's remaining maximum responses fit before starting its page enumeration.</summary>
    internal static void RequireReadCapacity(long bytes, long operations)
    {
        for (PayloadOperationBudget? budget = Current.Value; budget is not null; budget = budget._parent)
        {
            if (budget._readBytes + bytes > PayloadProtectionOperationalLimits.ReadBytes
                || budget._readOperations + operations > PayloadProtectionOperationalLimits.ReadOperations)
            { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderUnavailable, isAdmissionLimit: true); }
        }
    }
    /// <summary>Rejects a result returned after any enclosing monotonic deadline, even when a dependency ignored cancellation.</summary>
    internal static void RequireCurrent()
    {
        for (PayloadOperationBudget? budget = Current.Value; budget is not null; budget = budget._parent)
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
