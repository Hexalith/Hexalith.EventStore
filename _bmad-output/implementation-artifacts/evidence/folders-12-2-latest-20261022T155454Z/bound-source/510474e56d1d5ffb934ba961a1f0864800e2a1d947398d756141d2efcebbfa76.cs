using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Shares guarded current and frozen-tail delivery between immediate, poller and retry entry points.</summary>
internal interface IProjectionSourceGuardDelivery
{
    /// <summary>Uses one verified catalog snapshot and owning source/row/model/lifecycle seams.</summary>
    Task<ProjectionSourceGuardDeliveryResult> TryDeliverAsync(AggregateIdentity identity, CancellationToken cancellationToken);

    /// <summary>Uses the retry's explicit count profile and settles independently proven original routes before starting later effects.</summary>
    Task<ProjectionSourceGuardDeliveryResult> TryDeliverAsync(AggregateIdentity identity, ProjectionDeliveryRetryWorkItem original,
        int pageSize, CancellationToken cancellationToken);

    /// <summary>Inspects current evidence only; never starts a handler, recovery, cleanup or mutation.</summary>
    Task<ProjectionSourceGuardDeliveryResult> InspectCurrentAsync(AggregateIdentity identity, CancellationToken cancellationToken);

    /// <summary>Also fails closed for the queried persisted scope after catalog route removal, without mutating it.</summary>
    Task<ProjectionSourceGuardDeliveryResult> InspectCurrentAsync(AggregateIdentity identity, string projection,
        CancellationToken cancellationToken);

    /// <summary>Separately reconciles private material after an independently proven current result.</summary>
    Task<bool> ReclaimCompletedReplayAsync(AggregateIdentity identity, CancellationToken cancellationToken);

    /// <summary>Reclaims only the independently proven pending routes of the exact original durable work.</summary>
    Task<bool> ReclaimCompletedReplayAsync(AggregateIdentity identity, ProjectionDeliveryRetryWorkItem original,
        IReadOnlyList<string> routes, CancellationToken cancellationToken);
}
