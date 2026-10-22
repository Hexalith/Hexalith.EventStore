using System.Collections.Concurrent;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Counts actual fixture app callbacks using closed categories, without retaining addressed actor paths.</summary>
public sealed class LiveNativeCallbackCounter
{
    private readonly ConcurrentDictionary<string, long> _counts = new(StringComparer.Ordinal);

    internal void Observe(string path)
    {
        if (path == "/healthz") return;
        string category = path.StartsWith("/actors/", StringComparison.Ordinal)
            ? path.Contains("/method/", StringComparison.Ordinal) ? "actor_methods" : "actor_lifecycle"
            : path.StartsWith("/project/", StringComparison.Ordinal) ? "projections"
            : path == "/admin/operational-index-metadata" ? "metadata" : "other";
        _counts.AddOrUpdate(category, 1, static (_, value) => value + 1);
    }

    internal IReadOnlyDictionary<string, long> Snapshot() => _counts.ToDictionary(StringComparer.Ordinal);

    internal void ObserveMetadataBodies(long requestBytes, long responseBytes)
    {
        _counts.AddOrUpdate("metadata_request_bytes", requestBytes, (_, value) => value + requestBytes);
        _counts.AddOrUpdate("metadata_response_bytes", responseBytes, (_, value) => value + responseBytes);
    }
}
