// Approved Azure exception fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394 sections 3-7; base V127-V132.
using System.Collections.Concurrent;
using Hexalith.EventStore.PayloadProtection.AzureKeyVault.Conformance;
using System.Diagnostics.Tracing;
using System.Diagnostics;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests;
internal sealed class AzureDiagnosticObserver : EventListener, IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
{
    private readonly ConcurrentBag<AzureDiagnosticRecord> _records = [];
    private readonly List<IDisposable> _subscriptions = [];
    internal string[] Values
    {
        get
        {
            AzureIdentityDiagnosticPolicy.RequireCaptureIntegrity(Records);
            return _records.Select(record => record.Content).ToArray();
        }
    }
    internal AzureDiagnosticRecord[] Records => _records.ToArray();

    internal string AssertSafe(IReadOnlyList<string> identities, IReadOnlyList<string> prohibited)
        => AzureIdentityDiagnosticPolicy.AssertSafe(Records, identities, prohibited);

    internal void Record(string value) => _records.Add(new("Other", "observer", "Record", null, [value], AzureIdentityDiagnosticScope.Current));
    protected override void OnEventSourceCreated(EventSource source)
    {
        if (source.Name.StartsWith("Azure-", StringComparison.Ordinal) || source.Name.StartsWith("Microsoft-Identity", StringComparison.Ordinal) || source.Name.StartsWith("Microsoft.Identity", StringComparison.Ordinal) || source.Name == "System.Net.Http")
        {
            EnableEvents(source, EventLevel.Verbose, EventKeywords.All);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs data)
    {
        if (_records is not null)
        {
            _records.Add(new("EventSource", data.EventSource.Name, data.EventName ?? "", data.EventId,
                data.Payload?.Select(AzureDiagnosticRecord.Capture).ToArray() ?? [], AzureIdentityDiagnosticScope.Current));
        }
    }

    public void OnNext(DiagnosticListener listener)
    {
        if (listener.Name.StartsWith("Azure.", StringComparison.Ordinal) || listener.Name.StartsWith("Microsoft.Identity", StringComparison.Ordinal) || listener.Name == "HttpHandlerDiagnosticListener")
        {
            _subscriptions.Add(listener.Subscribe(this));
        }
    }

    public void OnNext(KeyValuePair<string, object?> value) => _records.Add(new("DiagnosticListener", "observer", value.Key, null, [AzureDiagnosticRecord.Capture(value.Value)], AzureIdentityDiagnosticScope.Current));
    public void OnError(Exception error) => _records.Add(new("Exception", "observer", "OnError", null, [error.ToString()], AzureIdentityDiagnosticScope.Current));
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
