namespace Hexalith.EventStore.Extensions;

/// <summary>Marks endpoints mapped by the EventStore's Dapr actor runtime.</summary>
public sealed class EventStoreActorCallbackEndpointMetadata
{
    private EventStoreActorCallbackEndpointMetadata()
    {
    }

    /// <summary>Gets the marker shared by the actual actor runtime endpoint mappings.</summary>
    public static EventStoreActorCallbackEndpointMetadata Instance { get; } = new();
}
