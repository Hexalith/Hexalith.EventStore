namespace Hexalith.EventStore.Server.Events;

using Hexalith.EventStore.Contracts.Identity;

/// <summary>
/// Reads events from the actor state store and rehydrates aggregate state.
/// </summary>
public interface IEventStreamReader
{
    /// <summary>
    /// Rehydrates aggregate state by replaying all events from the event stream.
    /// </summary>
    /// <param name="identity">The aggregate identity providing key derivation.</param>
    /// <returns>The rehydrated state object, or null for new aggregates with no events.</returns>
    Task<object?> RehydrateAsync(AggregateIdentity identity);
}
