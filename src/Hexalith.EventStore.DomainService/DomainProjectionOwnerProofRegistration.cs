using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>Explicit platform proof enrollment of one exact route, independent of marker interfaces.</summary>
/// <param name="Domain">The exact domain.</param>
/// <param name="ProjectionType">The named projection.</param>
/// <param name="Capability">The fingerprint-bound handler and recovery semantics.</param>
/// <param name="Binding">The owning SDK selection/receipt binding, kept out of wire DTOs.</param>
public sealed record DomainProjectionOwnerProofRegistration(string Domain, string ProjectionType,
    ProjectionSourceGuardCapability Capability, ReadModelOwnerProofBinding Binding);
