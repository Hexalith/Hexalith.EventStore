using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Admin.Server.Tests.Helpers;

/// <summary>
/// Captures every Log call so tests can assert on rendered messages and exceptions. Used in
/// tests that verify log records do not echo secrets (AC1 truth contract): the log channel is
/// part of the secret-leak surface, and `NullLogger` discards everything which makes the
/// assertion vacuous.
/// </summary>
internal sealed class RecordingLogger<T> : ILogger<T> {
    private readonly List<LogRecord> _records = [];

    public IReadOnlyList<LogRecord> Records => _records;

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) {
        ArgumentNullException.ThrowIfNull(formatter);
        _records.Add(new LogRecord(
            logLevel,
            formatter(state, exception),
            exception));
    }

    internal sealed record LogRecord(LogLevel Level, string Message, Exception? Exception);

    /// <summary>
    /// No-op disposable returned from <see cref="BeginScope{TState}"/>. Returning a sentinel
    /// instead of <c>null</c> keeps third-party log wrappers that do
    /// <c>scope.Dispose();</c> without a null-check from NREing under this test harness.
    /// </summary>
    private sealed class NullScope : IDisposable {
        public static readonly NullScope Instance = new();

        public void Dispose() {
            // intentional no-op
        }
    }
}
