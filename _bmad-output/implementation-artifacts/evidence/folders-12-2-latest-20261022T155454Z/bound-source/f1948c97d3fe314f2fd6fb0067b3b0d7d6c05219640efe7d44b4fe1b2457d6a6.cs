namespace Hexalith.EventStore.Client.Events;

/// <summary>Receives the host's immutable event evolution registry after aggregate construction.</summary>
internal interface IEventPayloadEvolutionAware
{
    /// <summary>Gets or sets the host registry.</summary>
    EventPayloadEvolutionRegistry? EvolutionRegistry { get; set; }
}
