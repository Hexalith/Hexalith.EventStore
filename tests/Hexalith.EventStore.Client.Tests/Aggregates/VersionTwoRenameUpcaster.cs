using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Tests.Events;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Renames a version-one event and converts its old payload property.</summary>
internal sealed class VersionTwoRenameUpcaster : IEventPayloadUpcaster
{
    /// <inheritdoc/>
    public string EventTypeName => "Old.Contracts.ValueRaised";

    /// <inheritdoc/>
    public int FromVersion => 1;

    /// <inheritdoc/>
    public string TargetEventTypeName => typeof(VersionTwoTestEvent).FullName!;

    /// <inheritdoc/>
    public JsonObject Upcast(JsonObject payload)
    {
        payload["Value"] = payload["Amount"]!.GetValue<int>();
        payload.Remove("Amount");
        return payload;
    }
}
