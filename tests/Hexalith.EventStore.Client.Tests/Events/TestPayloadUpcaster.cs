using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Client.Tests.Events;

/// <summary>Configurable pure upcaster for registry boundary tests.</summary>
internal sealed class TestPayloadUpcaster(string name, int version, string? target,
    Func<JsonObject, JsonObject> transform) : IEventPayloadUpcaster
{
    /// <inheritdoc/>
    public string EventTypeName => name;

    /// <inheritdoc/>
    public int FromVersion => version;

    /// <inheritdoc/>
    public string? TargetEventTypeName => target;

    /// <inheritdoc/>
    public JsonObject Upcast(JsonObject payload) => transform(payload);
}
