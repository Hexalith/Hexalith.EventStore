#if SELECTED
using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Events;

namespace PublishedConsumer;

/// <summary>Converts the stored version-one amount to the current value without changing input bytes.</summary>
public sealed class IncrementUpcaster : IEventPayloadUpcaster
{
    /// <inheritdoc/>
    public string EventTypeName => typeof(EvolvedIncremented).FullName!;
    /// <inheritdoc/>
    public int FromVersion => 1;

    /// <inheritdoc/>
    public JsonObject Upcast(JsonObject payload)
    {
        payload["Value"] = payload["Amount"]!.GetValue<int>();
        payload.Remove("Amount");
        return payload;
    }
}
#endif
