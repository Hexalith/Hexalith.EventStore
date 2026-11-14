using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Tests.Telemetry;

/// <summary>Captures only the observation's structured fields and can reject advisory logging.</summary>
internal sealed class ShutdownStageTestLogger<T> : ILogger<T>
{
    /// <summary>Gets captured fixed-field records.</summary>
    internal ConcurrentQueue<IReadOnlyDictionary<string, object?>> Records { get; } = new();

    /// <summary>Gets or sets whether Debug observations are enabled.</summary>
    internal bool Enabled { get; set; } = true;

    /// <summary>Gets or sets whether the existing logging seam throws.</summary>
    internal bool Throw { get; set; }

    /// <summary>Gets the count of logger enablement checks.</summary>
    internal int EnableChecks { get; private set; }

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel)
    {
        EnableChecks++;
        if (Throw) throw new InvalidOperationException("private-logger-failure");
        return Enabled && logLevel == LogLevel.Debug;
    }

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => throw new InvalidOperationException("Shutdown observations must not create scopes.");

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (Throw) throw new InvalidOperationException("private-logger-failure");
        if (exception is not null || eventId.Id != 6210 || logLevel != LogLevel.Debug)
            throw new InvalidOperationException("Unexpected observation surface.");
        Records.Enqueue(((IEnumerable<KeyValuePair<string, object?>>)(object)state!).ToDictionary(static pair => pair.Key, static pair => pair.Value));
    }
}
