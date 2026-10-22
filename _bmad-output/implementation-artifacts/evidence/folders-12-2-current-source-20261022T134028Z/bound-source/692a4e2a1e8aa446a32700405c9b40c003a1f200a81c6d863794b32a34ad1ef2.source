using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Client.TestContracts;

/// <summary>Defines a current-name first step alongside a historical chain.</summary>
public sealed class ChainedEventCurrentUpcaster : IEventPayloadUpcaster
{
    /// <inheritdoc />
    public string EventTypeName => typeof(ChainedEvent).FullName!;

    /// <inheritdoc />
    public int FromVersion => 1;

    /// <inheritdoc />
    public JsonObject Upcast(JsonObject payload) => payload;
}
