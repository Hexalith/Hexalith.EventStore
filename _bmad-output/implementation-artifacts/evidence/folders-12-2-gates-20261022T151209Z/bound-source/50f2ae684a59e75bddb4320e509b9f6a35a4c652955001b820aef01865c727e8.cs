using System.Text.Json.Nodes;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Transforms one JSON event payload version into the next version.</summary>
public interface IEventPayloadUpcaster
{
    /// <summary>Gets the stored event name accepted by this step.</summary>
    string EventTypeName { get; }

    /// <summary>Gets the input version accepted by this step.</summary>
    int FromVersion { get; }

    /// <summary>Gets an optional renamed event name for the output.</summary>
    string? TargetEventTypeName => null;

    /// <summary>Transforms one JSON object into the next version without external side effects.</summary>
    JsonObject Upcast(JsonObject payload);
}
