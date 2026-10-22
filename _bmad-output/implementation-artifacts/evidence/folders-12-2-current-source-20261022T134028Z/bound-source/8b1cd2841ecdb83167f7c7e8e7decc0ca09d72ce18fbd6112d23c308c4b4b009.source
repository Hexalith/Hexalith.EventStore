using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Client.Tests.Events;

/// <summary>Parameterless upcaster used to verify discovery and explicit registration de-duplication.</summary>
internal sealed class VersionTwoTestUpcaster : IEventPayloadUpcaster
{
    /// <inheritdoc/>
    public string EventTypeName => typeof(VersionTwoTestEvent).FullName!;

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
