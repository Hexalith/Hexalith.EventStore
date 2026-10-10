using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Client.TestContracts;

/// <summary>Upcasts the separate-assembly event fixture.</summary>
public sealed class ExternalVersionedEventUpcaster : IEventPayloadUpcaster
{
    /// <inheritdoc />
    public string EventTypeName => typeof(ExternalVersionedEvent).FullName!;

    /// <inheritdoc />
    public int FromVersion => 1;

    /// <inheritdoc />
    public JsonObject Upcast(JsonObject payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        payload["Value"] = payload["Amount"]!.GetValue<int>();
        payload.Remove("Amount");
        return payload;
    }
}
