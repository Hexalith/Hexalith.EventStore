using Dapr.Actors;

using Hexalith.EventStore.Server.Identity;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Private proof-gated tenant registry; direct login identifiers never reach durable state.</summary>
public interface IIdentityActorRegistryActor : IActor
{
    /// <summary>Reads an existing alias or stable actor using exact signed scope; never enrolls.</summary>
    Task<ActorRegistryEntry?> ReadAsync(string lookup, bool byAlias, string messageId, string proof);

    /// <summary>Atomically applies an admitted trusted-service mutation and its original retry result.</summary>
    Task<ActorRegistryEntry> MutateAsync(ActorRegistryMutation mutation, string proof);
}
