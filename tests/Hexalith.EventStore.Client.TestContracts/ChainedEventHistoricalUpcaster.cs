using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Client.TestContracts;

/// <summary>Starts the historical long-name chain.</summary>
public sealed class ChainedEventHistoricalUpcaster : IEventPayloadUpcaster
{
    /// <inheritdoc />
    public string EventTypeName => "Historical.ChainedEvent";

    /// <inheritdoc />
    public int FromVersion => 1;

    /// <inheritdoc />
    public JsonObject Upcast(JsonObject payload) => payload;
}
