using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Captures formatted routing metadata for diagnostic confidentiality assertions.</summary>
internal sealed class Oq8DiagnosticRecordingLogger : ILogger<Oq8InvocationDiagnosticHandler>
{
    /// <summary>Gets the formatted diagnostic entries.</summary>
    public List<string> Entries { get; } = [];

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Entries.Add(formatter(state, exception));
}
