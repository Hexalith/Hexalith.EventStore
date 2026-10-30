using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.SignalR.Tests;

/// <summary>Pauses receipt logging after dispatch captures its callback snapshot.</summary>
internal sealed class StalledProjectionReceiptLogger : ILogger<EventStoreSignalRClient>, IDisposable
{
    /// <summary>Gets notification that the dispatch snapshot has been captured.</summary>
    public TaskCompletionSource Captured { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the release gate for the captured dispatch.</summary>
    public ManualResetEventSlim Resume { get; } = new(false);

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (eventId.Id is 2090 or 2091)
        {
            Captured.TrySetResult();
            if (!Resume.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Dispatch snapshot was not released.");
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => Resume.Dispose();
}
