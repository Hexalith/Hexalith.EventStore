using System.Text.Json;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Serialization;

namespace Hexalith.EventStore.Client.Handlers;

/// <summary>Reuses SDK state reconstruction for domain-specific admitted command dispatch.</summary>
public static class DomainStateReplay
{
    /// <summary>Reads the SDK snapshot-aware source wrapper after a service JSON handoff.</summary>
    public static DomainServiceCurrentState? SnapshotAware(object? currentState)
        => currentState is DomainServiceCurrentState typed ? typed
            : currentState is JsonElement { ValueKind: JsonValueKind.Object } json
                && json.TryGetProperty("events", out _) && json.TryGetProperty("currentSequence", out _)
                    ? json.Deserialize<DomainServiceCurrentState>(EventStorePayloadSerialization.Options)
                    : null;

    /// <summary>Reconstructs typed state using the same snapshot and Apply conventions as aggregates.</summary>
    public static TState? Rehydrate<TState>(object? currentState) where TState : class, new()
        => DomainProcessorStateRehydrator.RehydrateState<TState>(currentState,
            DomainProcessorStateRehydrator.DiscoverApplyMethods(typeof(TState)));
}
