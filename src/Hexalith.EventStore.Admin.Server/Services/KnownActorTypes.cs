namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// Describes a known DAPR actor type and its state keys for inspection.
/// </summary>
/// <param name="Description">A human-readable description of the actor type's purpose.</param>
/// <param name="ActorIdFormat">The expected format for actor IDs of this type.</param>
/// <param name="StateKeys">The state keys used by this actor type. Keys containing {N}, {correlationId}, etc. are dynamic families.</param>
public record KnownActorTypeDescriptor(
    string Description,
    string ActorIdFormat,
    IReadOnlyList<string> StateKeys);

/// <summary>
/// Static registry of known DAPR actor types and their state keys.
/// Used by the actor inspector to enumerate readable state keys for a given actor type.
/// </summary>
public static class KnownActorTypes {
    /// <summary>
    /// The known actor types registered in the EventStore server.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, KnownActorTypeDescriptor> Types =
        new Dictionary<string, KnownActorTypeDescriptor>(StringComparer.Ordinal) {
            ["AggregateActor"] = new(
                "Processes commands and persists events for domain aggregates",
                "tenant:domain:aggregate-id",
                [
                    "pending_command_count",
                    "{actorId}:metadata",
                    "{actorId}:snapshot",
                    "{actorId}:events:{N}",
                    "idempotency:{causationId}",
                    "{actorId}:pipeline:{correlationId}",
                    "drain:{correlationId}",
                ]),
            ["ETagActor"] = new(
                "Manages projection ETag values for cache invalidation",
                "ProjectionType:TenantId",
                ["etag"]),
            ["ProjectionActor"] = new(
                "Handles projection queries with in-memory page caching",
                "QueryType:TenantId[:EntityId]",
                ["projection-state"]),
        };

    /// <summary>
    /// Gets the descriptor for a known actor type, or a default descriptor for unknown types.
    /// </summary>
    /// <param name="actorType">The actor type name.</param>
    /// <returns>The descriptor, or a default for unknown types.</returns>
    public static KnownActorTypeDescriptor GetDescriptor(string actorType)
        => Types.TryGetValue(actorType, out KnownActorTypeDescriptor? descriptor)
            ? descriptor
            : new KnownActorTypeDescriptor("Unknown actor type", "actor-id", []);

    /// <summary>
    /// Determines whether the given state key is a dynamic key family (contains interpolation patterns).
    /// </summary>
    /// <param name="stateKey">The state key to check.</param>
    /// <returns>True if the key is a dynamic family pattern.</returns>
    public static bool IsDynamicKeyFamily(string stateKey) {
        ArgumentNullException.ThrowIfNull(stateKey);

        // Remove all {actorId} occurrences and check if other patterns remain
        string withoutActorId = stateKey.Replace("{actorId}", string.Empty, StringComparison.Ordinal);
        return withoutActorId.Contains('{');
    }

    /// <summary>
    /// Resolves a state key by substituting {actorId} with the actual actor ID.
    /// Returns null for dynamic key families that cannot be resolved.
    /// </summary>
    /// <param name="stateKey">The state key pattern.</param>
    /// <param name="actorId">The actor instance ID.</param>
    /// <returns>The resolved key, or null if the key is a dynamic family.</returns>
    public static string? ResolveStateKey(string stateKey, string actorId) {
        ArgumentNullException.ThrowIfNull(stateKey);
        ArgumentNullException.ThrowIfNull(actorId);

        if (!stateKey.Contains('{')) {
            return stateKey;
        }

        if (stateKey.Contains("{actorId}", StringComparison.Ordinal)) {
            string resolved = stateKey.Replace("{actorId}", actorId, StringComparison.Ordinal);

            // If after substitution there are still unresolved patterns, it's a family
            return resolved.Contains('{') ? null : resolved;
        }

        // Contains other patterns — dynamic family
        return null;
    }
}
