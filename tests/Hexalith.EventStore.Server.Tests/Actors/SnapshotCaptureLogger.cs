using Hexalith.EventStore.Server.Actors;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>Captures rendered snapshot diagnostics while pooled logging state is still valid.</summary>
internal sealed class SnapshotCaptureLogger : ILogger<AggregateActor>
{
    /// <summary>Gets rendered log messages from the actor.</summary>
    internal List<string> Messages { get; } = [];

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
        => Messages.Add(formatter(state, exception));
}
