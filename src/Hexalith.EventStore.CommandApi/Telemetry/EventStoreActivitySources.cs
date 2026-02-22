
using System.Diagnostics;

namespace Hexalith.EventStore.CommandApi.Telemetry;

internal static class EventStoreActivitySources {
    public const string Submit = "EventStore.CommandApi.Submit";
    public const string QueryStatus = "EventStore.CommandApi.QueryStatus";
    public const string Replay = "EventStore.CommandApi.Replay";

    public static readonly ActivitySource CommandApi = new("Hexalith.EventStore.CommandApi");
}
