using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>Captures snapshot-manager diagnostics and their exception arguments.</summary>
internal sealed class SnapshotManagerCaptureLogger : ILogger<SnapshotManager>
{
    /// <summary>Gets rendered messages.</summary>
    internal List<string> Messages { get; } = [];

    /// <summary>Gets event IDs associated with each message.</summary>
    internal List<EventId> EventIds { get; } = [];

    /// <summary>Gets exception arguments associated with each message.</summary>
    internal List<Exception?> Exceptions { get; } = [];

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Messages.Add(formatter(state, exception));
        EventIds.Add(eventId);
        Exceptions.Add(exception);
    }
}
