using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Client.TestContracts;

/// <summary>Continues a historical chain through a short event name.</summary>
public sealed class ChainedEventNextUpcaster : IEventPayloadUpcaster
{
    /// <inheritdoc />
    public string EventTypeName => nameof(ChainedEvent);

    /// <inheritdoc />
    public int FromVersion => 2;

    /// <inheritdoc />
    public JsonObject Upcast(JsonObject payload) => payload;
}
