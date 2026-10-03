namespace Hexalith.EventStore.Server.Identity;

/// <summary>One serialized tenant registry namespace, owning aliases, actors and retry results atomically.</summary>
/// <param name="Aliases">Purpose-keyed alias digest to stable actor mappings.</param>
/// <param name="Actors">Current actor capabilities.</param>
/// <param name="Operations">Immutable retry identities and their original results.</param>
public sealed record ActorRegistryState(Dictionary<string, ActorRegistryAlias> Aliases,
    Dictionary<string, ActorRegistryEntry> Actors, Dictionary<string, ActorRegistryOperation> Operations);
