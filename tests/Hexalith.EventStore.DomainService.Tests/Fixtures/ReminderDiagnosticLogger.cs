using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.DomainService.Tests.Fixtures;

/// <summary>Captures the structured state and formatter output actually emitted by the reminder runtime.</summary>
/// <typeparam name="T">The logger category.</typeparam>
internal sealed class ReminderDiagnosticLogger<T> : ILogger<T>
{
    /// <summary>Gets the emitted event metadata, fields, exception, and formatted message.</summary>
    public List<(EventId EventId, LogLevel Level, IReadOnlyDictionary<string, object?> Fields, Exception? Exception, string Message)> Entries { get; } = [];

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var fields = ((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary(static field => field.Key, static field => field.Value);
        Entries.Add((eventId, logLevel, fields, exception, formatter(state, exception)));
    }
}
