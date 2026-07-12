namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// A domain-declared logical projection read-model slot: which projection it belongs to, its logical slot
/// name, and whether it is aggregate-owned (erasable) or shared (excluded from whole-key erasure). Domains
/// declare these instead of implementing raw DAPR erasure plumbing (AD-2).
/// </summary>
/// <param name="ProjectionName">The projection the slot belongs to (colon-free segment).</param>
/// <param name="Slot">The logical slot name (colon-free segment).</param>
/// <param name="Kind">Whether the slot is aggregate-owned or shared.</param>
public sealed record ProjectionReadModelSlotDeclaration(
    string ProjectionName,
    string Slot,
    ProjectionReadModelSlotKind Kind);

/// <summary>
/// Implemented by a domain projection type to declare its logical read-model slots. The declaration is read
/// at registration time by <c>AddDomainProjectionHandlers</c> without instantiating the type, and each
/// declared slot is registered with the platform slot registry.
/// </summary>
public interface IDeclaresProjectionReadModelSlots {
    /// <summary>Gets the read-model slots declared by this projection.</summary>
    static abstract IReadOnlyList<ProjectionReadModelSlotDeclaration> ProjectionReadModelSlots { get; }
}
