using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

/// <summary>Moves the stored version-one actor event to its current JSON shape.</summary>
internal sealed class VersionedActorUpcaster : IEventPayloadUpcaster
{
    /// <inheritdoc/>
    public string EventTypeName => typeof(VersionedActorEvent).FullName!;

    /// <inheritdoc/>
    public int FromVersion => 1;

    /// <summary>Gets the number of upcast calls for the round-trip proof.</summary>
    internal int Calls { get; private set; }

    /// <inheritdoc/>
    public JsonObject Upcast(JsonObject payload)
    {
        Calls++;
        payload["Value"] = payload["Amount"]!.GetValue<int>();
        payload.Remove("Amount");
        return payload;
    }
}
