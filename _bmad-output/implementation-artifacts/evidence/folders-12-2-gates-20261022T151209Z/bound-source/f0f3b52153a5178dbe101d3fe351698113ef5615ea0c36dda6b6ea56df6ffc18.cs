using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Tests.TestUtilities;

/// <summary>
/// Captures formatted log messages emitted by an in-process host.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _messages = new();

    /// <summary>Gets the captured messages.</summary>
    public IReadOnlyCollection<string> Messages => [.. _messages];

    /// <summary>Clears the captured messages.</summary>
    public void Clear() => _messages.Clear();

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(_messages);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            messages.Enqueue(formatter(state, exception) + (exception is null ? string.Empty : " | " + exception));
        }
    }
}
