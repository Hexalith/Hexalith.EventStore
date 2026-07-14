using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Mints canonical <see cref="ProjectionReadModelAddress"/> values from a validated aggregate identity,
/// projection name, and a registered aggregate-owned slot. This is the only sanctioned producer of
/// erasable read-model addresses; legacy/opaque caller keys and shared/index slots are denied.
/// </summary>
public interface IProjectionReadModelAddressFactory {
    /// <summary>
    /// Creates the canonical address for a single aggregate-owned slot.
    /// </summary>
    /// <param name="identity">The validated aggregate identity.</param>
    /// <param name="projectionName">The validated projection name.</param>
    /// <param name="slot">The registered aggregate-owned logical slot.</param>
    /// <returns>The canonical address.</returns>
    /// <exception cref="ArgumentException">An identity component, the projection, or the slot is blank or contains a reserved character.</exception>
    /// <exception cref="ProjectionReadModelAddressException">The slot is not registered, or is registered as shared.</exception>
    ProjectionReadModelAddress Create(AggregateIdentity identity, string projectionName, string slot);

    /// <summary>
    /// Recreates a canonical address for a manifest that was already admitted by the persisted lifecycle
    /// actor. The manifest digest remains the authority when a later deployment changes slot declarations.
    /// </summary>
    /// <param name="identity">The validated aggregate identity.</param>
    /// <param name="projectionName">The validated projection name.</param>
    /// <param name="slot">The logical slot recorded by the admitted request.</param>
    /// <returns>The canonical address.</returns>
    /// <remarks>
    /// The default preserves binary compatibility for custom factories and reuses their normal validation.
    /// Platform factories override this member to derive the already-admitted address without consulting
    /// mutable slot-registration metadata.
    /// </remarks>
    ProjectionReadModelAddress CreateForAdmittedManifest(
        AggregateIdentity identity,
        string projectionName,
        string slot) => Create(identity, projectionName, slot);

    /// <summary>
    /// Creates canonical addresses for every registered aggregate-owned slot of a projection — the whole-key
    /// erasure manifest for one aggregate identity. Shared slots are excluded.
    /// </summary>
    /// <param name="identity">The validated aggregate identity.</param>
    /// <param name="projectionName">The validated projection name.</param>
    /// <returns>The aggregate-owned addresses (possibly empty when no aggregate-owned slot is registered).</returns>
    IReadOnlyList<ProjectionReadModelAddress> CreateAggregateOwnedManifest(AggregateIdentity identity, string projectionName);
}
