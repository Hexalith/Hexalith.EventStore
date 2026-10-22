using System.Collections.Concurrent;
using System.Diagnostics;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Observes SDK sends to one owned loopback sidecar, including its uncustomizable actor-state interactor.</summary>
public sealed class LiveSdkHttpAttemptObserver : IDisposable, IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
{
    private readonly int _port;
    private readonly ConcurrentDictionary<string, long> _attempts = new(StringComparer.Ordinal);
    private readonly ConcurrentBag<IDisposable> _listeners = [];
    private readonly IDisposable _discovery;

    internal LiveSdkHttpAttemptObserver(int port)
    {
        _port = port;
        _discovery = DiagnosticListener.AllListeners.Subscribe(this);
    }

    void IObserver<DiagnosticListener>.OnNext(DiagnosticListener listener)
    {
        if (listener.Name == "HttpHandlerDiagnosticListener")
            _listeners.Add(listener.Subscribe(this, static name => name.StartsWith("System.Net.Http.HttpRequestOut", StringComparison.Ordinal)));
    }

    void IObserver<KeyValuePair<string, object?>>.OnNext(KeyValuePair<string, object?> diagnostic)
    {
        try
        {
            if (diagnostic.Key != "System.Net.Http.HttpRequestOut.Start"
                || diagnostic.Value?.GetType().GetProperty("Request")?.GetValue(diagnostic.Value) is not HttpRequestMessage request
                || request.RequestUri is not { IsLoopback: true } uri || uri.Port != _port) return;
            string path = uri.AbsolutePath;
            string category;
            if (path.StartsWith("/v1.0/actors/", StringComparison.Ordinal))
                category = path.Contains("/method/", StringComparison.Ordinal) ? "actor_method"
                    : path.Contains("/state", StringComparison.Ordinal) ? request.Method == HttpMethod.Get
                        ? "actor_state_get" : "actor_state_transaction" : "actor_other";
            else if (path.StartsWith("/v1.0/invoke/", StringComparison.Ordinal)) category = "service_invocation";
            else if (path.StartsWith("/v1.0/state/", StringComparison.Ordinal)) category = "state_api";
            else return;
            // Nothing from the request, address or diagnostic payload escapes this callback.
            _attempts.AddOrUpdate(category, 1, static (_, count) => count + 1);
        }
        catch { /* A diagnostic callback never alters the observed HTTP operation. */ }
    }

    internal IReadOnlyDictionary<string, long> Snapshot() => _attempts.ToDictionary(StringComparer.Ordinal);
    void IObserver<DiagnosticListener>.OnCompleted() { }
    void IObserver<DiagnosticListener>.OnError(Exception error) { }
    void IObserver<KeyValuePair<string, object?>>.OnCompleted() { }
    void IObserver<KeyValuePair<string, object?>>.OnError(Exception error) { }

    public void Dispose()
    {
        _discovery.Dispose();
        foreach (IDisposable listener in _listeners) listener.Dispose();
    }
}
