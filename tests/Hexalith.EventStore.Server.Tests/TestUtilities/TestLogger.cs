using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Tests.TestUtilities;

/// <summary>
/// Captures log entries into the supplied list for assertion in tests. Replaces per-test-file
/// duplications of identical capture-logger shapes.
/// </summary>
internal sealed class TestLogger<T>(List<LogEntry> entries, LogLevel minimumLevel = LogLevel.Trace) : ILogger<T> {
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= minimumLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception)));
        }
    }
}

internal sealed record LogEntry(LogLevel Level, EventId EventId, string Message);
