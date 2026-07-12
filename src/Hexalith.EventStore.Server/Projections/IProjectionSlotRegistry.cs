using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Registry of logical projection read-model slots declared by domain modules. The canonical address
/// factory consults it to decide whether a requested slot is aggregate-owned (erasable) or shared
/// (excluded from whole-key erasure), and to enumerate every aggregate-owned slot for a projection when
/// building the coordinated-erasure manifest.
/// </summary>
public interface IProjectionSlotRegistry {
    /// <summary>
    /// Registers a logical slot for a projection. Re-registering the same slot with the same kind is an
    /// idempotent no-op; re-registering it with a conflicting kind is a configuration error.
    /// </summary>
    /// <param name="projectionName">The projection name (colon-free segment).</param>
    /// <param name="slot">The logical slot name (colon-free segment).</param>
    /// <param name="kind">Whether the slot is aggregate-owned or shared.</param>
    void Register(string projectionName, string slot, ProjectionReadModelSlotKind kind);

    /// <summary>
    /// Looks up the declared kind of a slot.
    /// </summary>
    /// <param name="projectionName">The projection name.</param>
    /// <param name="slot">The logical slot name.</param>
    /// <param name="kind">The registered kind when found.</param>
    /// <returns><see langword="true"/> when the slot is registered; otherwise <see langword="false"/>.</returns>
    bool TryGetKind(string projectionName, string slot, out ProjectionReadModelSlotKind kind);

    /// <summary>
    /// Returns the logical slot names registered as <see cref="ProjectionReadModelSlotKind.AggregateOwned"/>
    /// for a projection, in a stable order. Shared slots are excluded.
    /// </summary>
    /// <param name="projectionName">The projection name.</param>
    /// <returns>The aggregate-owned slot names (possibly empty).</returns>
    IReadOnlyList<string> GetAggregateOwnedSlots(string projectionName);
}
