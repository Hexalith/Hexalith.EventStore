using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Tests.Routing;

internal sealed class CatalogRefusalLogCapture : ILoggerProvider, ILogger
{
    public List<string> Messages { get; } = [];
    public List<Exception?> Exceptions { get; } = [];
    public ILogger CreateLogger(string categoryName) => this;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Messages.Add(formatter(state, exception)); Exceptions.Add(exception);
    }
    public void Dispose() { }
}
