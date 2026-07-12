namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Declares whether a logical projection read-model slot is owned by a single aggregate or shared across
/// aggregates. Only aggregate-owned slots are erasable as whole keys during coordinated erasure.
/// </summary>
/// <remarks>
/// Lives in the Client package (referenced by domain modules and the DomainService SDK) so a domain can
/// declare its slots without depending on the Server platform package.
/// </remarks>
public enum ProjectionReadModelSlotKind {
    /// <summary>
    /// The slot holds one value per aggregate identity; the whole key is aggregate-owned and is erased when
    /// that aggregate's projection state is erased.
    /// </summary>
    AggregateOwned,

    /// <summary>
    /// The slot is shared across aggregates (for example a cross-aggregate index or singleton). It is
    /// structurally excluded from whole-key erasure and the address factory refuses to mint an erasable
    /// address for it.
    /// </summary>
    Shared,
}
