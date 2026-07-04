using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Server.Configuration;

/// <summary>
/// Dapr actor type-name options for EventStore server actors.
/// </summary>
public sealed class EventStoreActorOptions {
    /// <summary>
    /// Gets or sets the Dapr actor type name used for aggregate actors.
    /// </summary>
    public string AggregateActorTypeName { get; set; } = nameof(AggregateActor);
}
