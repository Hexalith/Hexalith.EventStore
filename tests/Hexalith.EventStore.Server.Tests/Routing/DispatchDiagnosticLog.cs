using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Captures complete enabled log output and exposes structured dispatch records separately.</summary>
internal sealed class DispatchDiagnosticLog(bool enabled = true, bool throwOnWrite = false) : ILoggerProvider, ILogger
{
    private readonly object _gate = new();
    private readonly List<IReadOnlyDictionary<string, object?>> _records = [];
    private readonly List<string> _output = [];
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal string Output { get { lock (_gate) return string.Join("\n", _output); } }

    internal IReadOnlyList<IReadOnlyDictionary<string, object?>> Records
    {
        get { lock (_gate) return _records.ToArray(); }
    }

    internal async Task WaitForAsync(string stage, string status)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            Task changed;
            lock (_gate)
            {
                if (_records.Any(record => Equals(record["Stage"], stage) && Equals(record["Status"], status))) return;
                changed = _changed.Task;
            }
            await changed.WaitAsync(timeout.Token);
        }
    }

    public ILogger CreateLogger(string categoryName) => this;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => enabled;
    public void Dispose() { }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_gate) _output.Add(formatter(state, exception) + (exception is null ? string.Empty : "\n" + exception));
        if (eventId.Id != 1190) return;
        if (throwOnWrite) throw new InvalidOperationException("diagnostic-provider-private-failure");
        var values = ((IEnumerable<KeyValuePair<string, object?>>)(object)state!).ToDictionary();
        values.Add("Exception", exception);
        lock (_gate)
        {
            _records.Add(values);
            _changed.TrySetResult();
            _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
