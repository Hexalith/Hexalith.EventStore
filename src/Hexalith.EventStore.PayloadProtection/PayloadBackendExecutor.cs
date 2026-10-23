// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 10.6-10.7 and 15.
using System.Security.Cryptography;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Executes typed backend requests with one shared budget and a backend/endpoint-scoped transient breaker.</summary>
internal sealed class PayloadBackendExecutor(TimeProvider? timeProvider = null,
    Func<TimeSpan, CancellationToken, ValueTask>? delay = null)
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private readonly object _gate = new();
    private readonly Dictionary<(string Backend, string Endpoint), PayloadBackendCircuitState> _circuits = [];

    /// <summary>Runs at most three confirmed-absent transient attempts; ambiguous mutations are returned for reconciliation.</summary>
    internal async ValueTask<PayloadBackendResult> ExecuteAsync(PayloadProtectionBackend backend,
        Func<CancellationToken, ValueTask<PayloadBackendResult>> request, CancellationToken cancellationToken = default,
        bool allowRetry = true)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = PayloadProtectionStateKeys.Sha(backend.EndpointFingerprint);
        _ = CanonicalText.GetByteCount(backend.BackendId, 1, 64);
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10), _clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, budget.Token);
        long started = _clock.GetTimestamp();
        for (int attempt = 0; attempt < (allowRetry ? 3 : 1); attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (budget.IsCancellationRequested || _clock.GetElapsedTime(started) >= TimeSpan.FromSeconds(10)) { return Unavailable(); }
            if (!TryEnter(backend, out bool probe)) { return Unavailable(); }
            PayloadBackendResult result;
            Task<PayloadBackendResult>? pending = null;
            try
            {
                pending = request(linked.Token).AsTask();
                result = await pending.WaitAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Abandon(pending);
                ReleaseCancelledProbe(backend, probe);
                throw;
            }
            catch
            {
                Abandon(pending);
                result = Unavailable();
            }
            if (cancellationToken.IsCancellationRequested)
            {
                result.Dispose();
                ReleaseCancelledProbe(backend, probe);
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (budget.IsCancellationRequested || _clock.GetElapsedTime(started) >= TimeSpan.FromSeconds(10))
            {
                result.Dispose();
                Observe(backend, PayloadBackendFailure.Unavailable, probe);
                return Unavailable();
            }
            Observe(backend, result.Failure, probe);
            if (result.Failure != PayloadBackendFailure.Unavailable || attempt == 2 || !allowRetry || probe) { return result; }
            TimeSpan pause = RetryDelay(attempt, result.RetryAfter, TimeSpan.FromSeconds(10) - _clock.GetElapsedTime(started));
            result.Dispose();
            if (pause < TimeSpan.Zero) { return Unavailable(); }
            try
            {
                if (delay is null) { await Task.Delay(pause, _clock, linked.Token).ConfigureAwait(false); }
                else { await delay(pause, linked.Token).ConfigureAwait(false); }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (OperationCanceledException) when (budget.IsCancellationRequested) { return Unavailable(); }
        }
        return Unavailable();
    }

    private bool TryEnter(PayloadProtectionBackend backend, out bool probe)
    {
        lock (_gate)
        {
            PayloadBackendCircuitState state = State(backend);
            probe = false;
            if (state.OpenedAt is not long opened) { return true; }
            if (_clock.GetElapsedTime(opened) < state.OpenInterval || state.ProbeRunning) { return false; }
            state.ProbeRunning = true;
            probe = true;
            return true;
        }
    }

    private void Observe(PayloadProtectionBackend backend, PayloadBackendFailure failure, bool probe)
    {
        lock (_gate)
        {
            PayloadBackendCircuitState state = State(backend);
            if (probe)
            {
                state.ProbeRunning = false;
                if (failure == PayloadBackendFailure.None)
                {
                    state.Failures.Clear();
                    state.OpenedAt = null;
                    state.OpenInterval = TimeSpan.FromSeconds(30);
                }
                else
                {
                    if (failure is PayloadBackendFailure.Unavailable or PayloadBackendFailure.Ambiguous)
                    {
                        state.OpenInterval = TimeSpan.FromSeconds(Math.Min(300, state.OpenInterval.TotalSeconds * 2));
                    }
                    state.OpenedAt = _clock.GetTimestamp();
                }
                return;
            }
            if (failure is not (PayloadBackendFailure.Unavailable or PayloadBackendFailure.Ambiguous)) { return; }
            while (state.Failures.TryPeek(out long timestamp) && _clock.GetElapsedTime(timestamp) >= TimeSpan.FromSeconds(30)) { state.Failures.Dequeue(); }
            state.Failures.Enqueue(_clock.GetTimestamp());
            if (state.Failures.Count >= 5 && state.OpenedAt is null) { state.OpenedAt = _clock.GetTimestamp(); }
            while (state.Failures.Count > 5) { state.Failures.Dequeue(); }
        }
    }

    private void ReleaseCancelledProbe(PayloadProtectionBackend backend, bool probe)
    {
        if (!probe) { return; }
        lock (_gate) { State(backend).ProbeRunning = false; }
    }
    private PayloadBackendCircuitState State(PayloadProtectionBackend backend)
    {
        var key = (backend.BackendId, backend.EndpointFingerprint);
        if (!_circuits.TryGetValue(key, out PayloadBackendCircuitState? state)) { _circuits.Add(key, state = new()); }
        return state;
    }
    private static PayloadBackendResult Unavailable() => new(PayloadBackendFailure.Unavailable);
    private static TimeSpan RetryDelay(int attempt, TimeSpan? retryAfter, TimeSpan remaining)
    {
        // Crypto jitter is inclusive of both ends of the approved +/-20 percent range.
        double milliseconds = (attempt == 0 ? 200 : 500) * (RandomNumberGenerator.GetInt32(800, 1201) / 1000d);
        TimeSpan pause = TimeSpan.FromMilliseconds(milliseconds);
        if (retryAfter is TimeSpan requested && requested >= TimeSpan.Zero && requested <= TimeSpan.FromSeconds(2) && requested < remaining) { pause = requested; }
        return pause < remaining ? pause : TimeSpan.FromTicks(-1);
    }
    private static void Abandon(Task<PayloadBackendResult>? task)
    {
        if (task is not null) { _ = DrainAbandonedAsync(task); }
    }
    private static async Task DrainAbandonedAsync(Task<PayloadBackendResult> task)
    {
        try { using PayloadBackendResult abandoned = await task.ConfigureAwait(false); }
        catch { /* Observe late faults without ever formatting provider exceptions. */ }
    }
}
