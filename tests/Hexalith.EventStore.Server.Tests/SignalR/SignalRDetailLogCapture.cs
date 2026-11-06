using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Tests.SignalR;

/// <summary>Snapshots generated log state before the logging implementation can clear its pooled storage.</summary>
/// <typeparam name="T">The category of the logger under test.</typeparam>
internal sealed class SignalRDetailLogCapture<T> : ILogger<T>
{
    /// <summary>Gets the rendered, structured and exception portions of every captured event.</summary>
    public List<(LogLevel Level, EventId EventId, string Message, KeyValuePair<string, object?>[] State, Exception? Exception)> Entries { get; } = [];

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        KeyValuePair<string, object?>[] structured = state is IEnumerable<KeyValuePair<string, object?>> values ? [.. values] : [];
        Entries.Add((logLevel, eventId, formatter(state, exception), structured, exception));
    }
}
