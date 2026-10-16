using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Immutable references retained while C and the active reservation remain unchanged before Idle binding.</summary>
internal sealed record ProjectionDeliveryIdleBindingPending(long FencingToken, long AdmittedHead,
    string LifecycleOperationToken, ProjectionOwnerProof OwnerProof);
