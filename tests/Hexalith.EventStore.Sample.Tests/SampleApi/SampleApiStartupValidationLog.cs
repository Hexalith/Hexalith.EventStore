using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Sample.Tests.SampleApi;

/// <summary>Observes the startup failure even when the test host has already disposed its services.</summary>
internal sealed class SampleApiStartupValidationLog : ILoggerProvider, ILogger
{
    private OptionsValidationException? _failure;

    /// <summary>Gets the validation exception logged by the host.</summary>
    public OptionsValidationException? Failure => Volatile.Read(ref _failure);

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName)
        => categoryName == "Microsoft.Extensions.Hosting.Internal.Host" ? this : NullLogger.Instance;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => logLevel == LogLevel.Error;

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc/>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel) && exception is OptionsValidationException validation)
        {
            _ = Interlocked.CompareExchange(ref _failure, validation, null);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}
