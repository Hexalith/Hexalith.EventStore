// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 10.6 and 15.
using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Bounds strongly consistent reads to three total attempts and one monotonic ten-second budget; mutations never enter this path.</summary>
internal sealed class PayloadStateReadExecutor(TimeProvider? timeProvider = null,
    Func<TimeSpan, CancellationToken, ValueTask>? delay = null)
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    /// <summary>Retries only typed transient read failures and discards any late output after cancellation or budget expiry.</summary>
    internal async ValueTask<T> ExecuteAsync<T>(Func<CancellationToken, ValueTask<T>> read, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        long started = _clock.GetTimestamp();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10), _clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            Task<T>? pending = null;
            try
            {
                pending = read(linked.Token).AsTask();
                T result = await pending.WaitAsync(linked.Token).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (deadline.IsCancellationRequested || _clock.GetElapsedTime(started) >= TimeSpan.FromSeconds(10)) { throw Unavailable(); }
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                ObserveLate(pending);
                throw;
            }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested)
            {
                ObserveLate(pending);
                throw Unavailable();
            }
            catch (PayloadProtectionStateException failure) when (failure.Reason == UnreadableProtectedDataReason.ProviderUnavailable)
            {
                if (attempt == 2 || _clock.GetElapsedTime(started) >= TimeSpan.FromSeconds(10)) { throw; }
                TimeSpan pause = TimeSpan.FromMilliseconds((attempt == 0 ? 200 : 500) * (RandomNumberGenerator.GetInt32(800, 1201) / 1000d));
                if (pause >= TimeSpan.FromSeconds(10) - _clock.GetElapsedTime(started)) { throw Unavailable(); }
                try
                {
                    if (delay is null) { await Task.Delay(pause, _clock, linked.Token).ConfigureAwait(false); }
                    else { await delay(pause, linked.Token).ConfigureAwait(false); }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw Unavailable(); }
            }
        }
        throw Unavailable();
    }
    private static void ObserveLate<T>(Task<T>? task)
    {
        if (task is not null) { _ = ObserveAsync(task); }
    }
    private static async Task ObserveAsync<T>(Task<T> task)
    {
        try { _ = await task.ConfigureAwait(false); }
        catch { /* A late state-read failure never crosses the safe error boundary. */ }
    }
    private static PayloadProtectionStateException Unavailable() => new(UnreadableProtectedDataReason.ProviderUnavailable);
}
