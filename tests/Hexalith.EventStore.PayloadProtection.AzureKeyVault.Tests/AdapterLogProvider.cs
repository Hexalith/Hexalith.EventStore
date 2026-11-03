// Approved Azure exception fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394 sections 3-7; base V127-V132.
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests;
internal sealed class AdapterLogProvider(Action<string> record) : ILoggerProvider, ILogger
{
    public ILogger CreateLogger(string categoryName) => this;
    public void Dispose()
    {
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => record(formatter(state, exception));
}
