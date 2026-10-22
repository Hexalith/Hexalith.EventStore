namespace Hexalith.EventStore.Extensions;

/// <summary>Marks endpoints mapped by the EventStore's Dapr actor runtime.</summary>
internal sealed class EventStoreActorCallbackEndpointMetadata
{
    private EventStoreActorCallbackEndpointMetadata()
    {
    }

    /// <summary>Gets the marker shared by the actual actor runtime endpoint mappings.</summary>
    internal static EventStoreActorCallbackEndpointMetadata Instance { get; } = new();
}
