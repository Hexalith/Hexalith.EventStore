#if SELECTED
using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Events;

namespace PublishedConsumer;

/// <summary>Converts the stored version-one amount to the current value without changing input bytes.</summary>
public sealed class IncrementUpcaster : IEventPayloadUpcaster
{
    public string EventTypeName => typeof(EvolvedIncremented).FullName!;
    public int FromVersion => 1;

    public JsonObject Upcast(JsonObject payload)
    {
        payload["Value"] = payload["Amount"]!.GetValue<int>();
        payload.Remove("Amount");
        return payload;
    }
}
#endif
