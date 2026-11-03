// Approved Azure exception fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394 sections 3-7; base V127-V132.
using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using System.Diagnostics;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests;
internal sealed class AzureDiagnosticObserver : EventListener, IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
{
    private readonly ConcurrentBag<string> _values = [];
    private readonly List<IDisposable> _subscriptions = [];
    internal string[] Values => _values.ToArray();

    internal void Record(string value) => _values.Add(value);
    protected override void OnEventSourceCreated(EventSource source)
    {
        if (source.Name.StartsWith("Azure-", StringComparison.Ordinal) || source.Name.StartsWith("Microsoft-Identity", StringComparison.Ordinal) || source.Name.StartsWith("Microsoft.Identity", StringComparison.Ordinal) || source.Name == "System.Net.Http")
        {
            EnableEvents(source, EventLevel.Verbose, EventKeywords.All);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs data)
    {
        if (_values is not null)
        {
            _values.Add(data.EventSource.Name + ":" + data.EventName + ":" + (data.Payload is null ? "" : string.Join("|", data.Payload)));
        }
    }

    public void OnNext(DiagnosticListener listener)
    {
        if (listener.Name.StartsWith("Azure.", StringComparison.Ordinal) || listener.Name.StartsWith("Microsoft.Identity", StringComparison.Ordinal) || listener.Name == "HttpHandlerDiagnosticListener")
        {
            _subscriptions.Add(listener.Subscribe(this));
        }
    }

    public void OnNext(KeyValuePair<string, object?> value) => _values.Add(value.Key + ":" + value.Value);
    public void OnError(Exception error) => _values.Add(error.ToString());
    public void OnCompleted()
    {
    }

    public override void Dispose()
    {
        foreach (IDisposable subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        base.Dispose();
    }
}
