namespace Hexalith.EventStore.IntegrationTests.Helpers;

using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

/// <summary>
/// Shared ILoggerProvider for integration tests that captures log entries for assertion.
/// </summary>
public sealed class TestLogProvider : ILoggerProvider {
    private readonly ConcurrentQueue<TestLogEntry> _entries = [];

    public ILogger CreateLogger(string categoryName) => new TestLogger(_entries);

    public void Dispose() {
        // Nothing to dispose
    }

    public List<TestLogEntry> GetEntries() => [.. _entries];

    public void Clear() {
        while (_entries.TryDequeue(out _)) {
            // Drain the queue
        }
    }

    private sealed class TestLogger(ConcurrentQueue<TestLogEntry> entries) : ILogger {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            entries.Enqueue(new TestLogEntry(logLevel, formatter(state, exception)));
        }
    }
}

public record TestLogEntry(LogLevel Level, string Message);
