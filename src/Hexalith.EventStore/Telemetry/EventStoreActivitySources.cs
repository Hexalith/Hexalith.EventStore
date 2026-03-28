
using System.Diagnostics;

namespace Hexalith.EventStore.Telemetry;

internal static class EventStoreActivitySources {
    public const string Submit = "EventStore.Submit";
    public const string QueryStatus = "EventStore.QueryStatus";
    public const string Replay = "EventStore.Replay";

    public static readonly ActivitySource EventStore = new("Hexalith.EventStore");
}
