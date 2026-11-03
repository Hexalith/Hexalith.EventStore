// Approved Azure exception fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394 sections 3-7; base V127-V132.
using System.Collections.Concurrent;
using Hexalith.EventStore.PayloadProtection.AzureKeyVault.Conformance;
using System.Diagnostics.Tracing;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.LiveTests;
internal sealed class AzureLiveDiagnosticObserver : EventListener, IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
{
    private readonly ConcurrentBag<AzureDiagnosticRecord> _records = [];
    private readonly List<IDisposable> _subscriptions = [];
    private volatile bool _disposed;
    private readonly ConcurrentBag<string> _plaintexts = [];
    private readonly ActivityListener _activities;
    private readonly MeterListener _metrics;
    private readonly ILoggerFactory _logs;
    private readonly EventHandler<FirstChanceExceptionEventArgs> _exceptions;

    /// <summary>Owns the same real activity/metric/log/exception observations as the unit conformance probe.</summary>
    internal AzureLiveDiagnosticObserver()
    {
        _activities = new ActivityListener
        {
            ShouldListenTo = source => Observed(source.Name),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => Add("Activity", activity.Source.Name, activity.OperationName,
                [activity.DisplayName, ..activity.TagObjects.Select(tag => tag.Key + AzureDiagnosticRecord.Capture(tag.Value)),
                    ..activity.Baggage.Select(tag => tag.Key + tag.Value),
                    ..activity.Events.SelectMany(item => new[] { item.Name }.Concat(item.Tags.Select(tag => tag.Key + AzureDiagnosticRecord.Capture(tag.Value)))),
                    ..activity.Links.SelectMany(link => (link.Tags ?? []).Select(tag => tag.Key + AzureDiagnosticRecord.Capture(tag.Value)))]),
        };
        ActivitySource.AddActivityListener(_activities);
        _metrics = new MeterListener
        {
            InstrumentPublished = (instrument, listener) => { if (Observed(instrument.Meter.Name)) listener.EnableMeasurementEvents(instrument); },
        };
        _metrics.SetMeasurementEventCallback<byte>(Measurement);
        _metrics.SetMeasurementEventCallback<short>(Measurement);
        _metrics.SetMeasurementEventCallback<int>(Measurement);
        _metrics.SetMeasurementEventCallback<long>(Measurement);
        _metrics.SetMeasurementEventCallback<float>(Measurement);
        _metrics.SetMeasurementEventCallback<double>(Measurement);
        _metrics.SetMeasurementEventCallback<decimal>(Measurement);
        _metrics.Start();
        _logs = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace)
            .AddProvider(new AdapterLogProvider(value => Add("ApplicationLog", "Hexalith.EventStore.PayloadProtection", "Log", [value]))));
        Logger = _logs.CreateLogger("Hexalith.EventStore.PayloadProtection.Azure");
        _exceptions = (_, args) => Add("Exception", "AppDomain", "FirstChance", [args.Exception.ToString()]);
        AppDomain.CurrentDomain.FirstChanceException += _exceptions;
    }

    /// <summary>The fixture's actual application logger is observed by this owned provider.</summary>
    internal ILogger Logger { get; }

    private static bool Observed(string name) => name.Contains("Azure", StringComparison.Ordinal)
        || name.Contains("Http", StringComparison.Ordinal) || name == "Hexalith.EventStore.PayloadProtection";

    private void Add(string channel, string source, string name, IReadOnlyList<string> payload)
    {
        if (!_disposed) _records.Add(new(channel, source, name, null, payload, AzureIdentityDiagnosticScope.Current));
    }

    private void Measurement<T>(Instrument instrument, T measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        var payload = new List<string> { AzureDiagnosticRecord.Capture(measurement) };
        foreach (var tag in tags) payload.Add(tag.Key + AzureDiagnosticRecord.Capture(tag.Value));
        Add("Metric", instrument.Meter.Name, instrument.Name, payload);
    }

    /// <summary>Retains every actual test plaintext and decoded JSON scalar before the owned buffer is used.</summary>
    internal void RegisterPlaintext(ReadOnlySpan<byte> bytes)
    {
        string text = Encoding.UTF8.GetString(bytes);
        _plaintexts.Add(text);
        try
        {
            using var document = JsonDocument.Parse(text);
            void Visit(JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.String) _plaintexts.Add(element.GetString()!);
                else if (element.ValueKind == JsonValueKind.Object) foreach (var property in element.EnumerateObject()) Visit(property.Value);
                else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) Visit(item);
            }
            Visit(document.RootElement);
        }
        catch (JsonException) { }
    }

    /// <summary>Offline consumer seam uses the same registered-plaintext safety check as the live fixture.</summary>
    internal string AssertSafe(IReadOnlyList<string> identities, IReadOnlyList<string> prohibited)
        => AzureIdentityDiagnosticPolicy.AssertSafe(Records, identities, [..prohibited, .._plaintexts]);

    internal string[] Values
    {
        get
        {
            AzureIdentityDiagnosticPolicy.RequireCaptureIntegrity(Records);
            return _records.Select(record => record.Content).ToArray();
        }
    }
    internal AzureDiagnosticRecord[] Records => _records.ToArray();

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
        if (_records is not null && !_disposed)
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

    internal void AssertSafe(AzureLiveAuthority authority)
    {
        string[] secrets = [authority.VaultUri, new Uri(authority.VaultUri).Host, authority.KeyName,
            authority.InitialVersion, authority.RotationVersion, authority.DisabledVersion, authority.ExpiredVersion, authority.MissingVersion,
            ..new[] { authority.InitialVersion, authority.RotationVersion, authority.DisabledVersion, authority.ExpiredVersion, authority.MissingVersion }
                .Select(version => new Uri(version).Segments.Last()),
            "authorized-live-canary", ..AzureLiveCredential.Tokens,
            ..Hexalith.EventStore.Testing.Security.ProtectedDataLeakSentinel.All(),
            Environment.GetEnvironmentVariable("IDENTITY_HEADER") ?? "", Environment.GetEnvironmentVariable("MSI_SECRET") ?? ""];
        string summary = AssertSafe([authority.ManagedIdentityClientId, authority.DeniedManagedIdentityClientId], secrets);
        Console.WriteLine("Azure diagnostic categories: " + summary);
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        AppDomain.CurrentDomain.FirstChanceException -= _exceptions;
        _activities.Dispose();
        _metrics.Dispose();
        _logs.Dispose();
        foreach (IDisposable subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        base.Dispose();
    }
}
