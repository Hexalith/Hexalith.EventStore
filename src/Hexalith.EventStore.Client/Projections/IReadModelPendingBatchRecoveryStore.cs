namespace Hexalith.EventStore.Client.Projections;

/// <summary>Recovers an original conditional batch discovered from one of its pending logical keys.</summary>
public interface IReadModelPendingBatchRecoveryStore
{
    /// <summary>Reconciles only a pending batch owned by the supplied store/tenant/domain/aggregate/projection scope.
    /// Logical-key discovery ignores the scope's batch ID; the durable original identity and manifest are reused.
    /// A resolved result permits reloading fresh values/ETags and recomputing after a compensated conflict.</summary>
    Task<ReadModelPendingBatchRecoveryResult> RecoverPendingAsync(ReadModelBatchScope scope, string key,
        CancellationToken cancellationToken = default);

    /// <summary>Also permits compaction of a committed unguarded v1 batch without its manifest.
    /// The caller must independently know that every original operation is nonexpiring: historical v1
    /// envelopes do not retain relative TTL evidence. Never use this capability for an unknown writer.</summary>
    Task<ReadModelPendingBatchRecoveryResult> RecoverNonExpiringPendingAsync(ReadModelBatchScope scope, string key,
        CancellationToken cancellationToken = default) => RecoverPendingAsync(scope, key, cancellationToken);
}
