namespace Hexalith.EventStore.CommandApi.Telemetry;

using System.Diagnostics;

internal static class EventStoreActivitySources
{
    public static readonly ActivitySource CommandApi = new("Hexalith.EventStore.CommandApi");
}
