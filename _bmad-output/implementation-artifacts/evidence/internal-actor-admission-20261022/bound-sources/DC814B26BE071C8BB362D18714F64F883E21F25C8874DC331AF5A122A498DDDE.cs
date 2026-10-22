using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Completion evidence bound only by the final conditional transition after actual Idle readback.</summary>
internal sealed record ProjectionDeliveryGuardEvidence(SourceProof Source, string CatalogFingerprint,
    string HandlerSemantics, ProjectionOwnerProof OwnerProof, long IdleLifecycleRevision);
