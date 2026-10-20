namespace Hexalith.EventStore.Client.Projections;

/// <summary>Recovers an original conditional batch discovered from one of its pending logical keys.</summary>
public interface IReadModelPendingBatchRecoveryStore
{
    /// <summary>Reconciles only a pending batch owned by the supplied store/tenant/domain/aggregate/projection scope.
    /// Logical-key discovery ignores the scope's batch ID; the durable original identity and manifest are reused.
    /// A resolved result permits reloading fresh values/ETags and recomputing after a compensated conflict.</summary>
    Task<ReadModelPendingBatchRecoveryResult> RecoverPendingAsync(ReadModelBatchScope scope, string key,
        CancellationToken cancellationToken = default);
}
