using System.Collections.Concurrent;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Counts actual caller HTTP sends without reading or retaining actor message bodies.</summary>
public sealed class LiveActorSendCounter
{
    private static readonly HashSet<string> Methods = new(StringComparer.Ordinal)
    {
        "InspectSourceProofAsync", "ReadMeasuredEventsBoundedRangeAsync", "ReadEventsBoundedRangeAsync", "ReadEventsRangeAsync",
        "GetEventsAsync", "GetStreamMetadataAsync", "ProcessCommandAsync", "BootstrapSourceProofAsync",
        "ReadSnapshotAsync", "BeginDeliveryWriteAsync", "CompleteDeliveryWriteAsync", "BeginRebuildAsync", "CompleteRebuildAsync",
        "GetProjectionAsync", "GetETagAsync", "SetProjectionAsync",
    };
    private readonly ConcurrentDictionary<string, long> _sends = new(StringComparer.Ordinal);

    internal void Observe(HttpRequestMessage request)
    {
        string path = request.RequestUri?.AbsolutePath ?? string.Empty;
        string method = path.Contains("/method/", StringComparison.Ordinal) ? path[(path.LastIndexOf('/') + 1)..] : string.Empty;
        string category = Methods.Contains(method) ? method : "other_actor_call";
        _sends.AddOrUpdate(category, 1, static (_, count) => count + 1);
    }

    internal IReadOnlyDictionary<string, long> Snapshot() => _sends.ToDictionary(StringComparer.Ordinal);
}
