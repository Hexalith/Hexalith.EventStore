using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.ServiceDefaults;

/// <summary>Enforces the bounded public-query denial mitigation before response headers or bytes are committed.</summary>
internal static class ReadQueryDenialTiming
{
    private static readonly TimeSpan _minimumDuration = TimeSpan.FromMilliseconds(50);

    /// <summary>Captures monotonic admission time and gates the final safe not-found response.</summary>
    /// <param name="context">The admitted public query request.</param>
    internal static void Register(HttpContext context)
    {
        long admittedAt = TimeProvider.System.GetTimestamp();
        context.Response.OnStarting(static state =>
        {
            var (request, started) = ((HttpContext, long))state;
            return WaitAsync(request, TimeProvider.System, started);
        }, (context, admittedAt));
    }

    /// <summary>Waits only for the remaining floor; request cancellation keeps its original token and precedence.</summary>
    /// <param name="context">The actual request and final response status.</param>
    /// <param name="clock">The monotonic clock and asynchronous timer.</param>
    /// <param name="admittedAt">The timestamp captured before admission and owner work.</param>
    /// <returns>The asynchronous response-header gate.</returns>
    internal static async Task WaitAsync(HttpContext context, TimeProvider clock, long admittedAt)
    {
        if (context.Response.StatusCode != StatusCodes.Status404NotFound)
        {
            return;
        }

        CancellationToken cancellation = context.RequestAborted;
        cancellation.ThrowIfCancellationRequested();
        TimeSpan remaining;
        while ((remaining = _minimumDuration - clock.GetElapsedTime(admittedAt)) > TimeSpan.Zero)
        {
            // Task.Delay uses millisecond timers. Round the remainder up and recheck to avoid an early response.
            TimeSpan timerDuration = TimeSpan.FromMilliseconds(Math.Ceiling(remaining.TotalMilliseconds));
            await Task.Delay(timerDuration, clock, cancellation).ConfigureAwait(false);
        }
    }
}
