namespace Hexalith.EventStore.Client.Projections;

/// <summary>Settles registered committed work and retires private preparation for an exact aggregate during physical erasure.</summary>
public interface IReadModelRecoveryScopeStore
{
    /// <summary>Persists discoverable preparation without installing any canonical participant.</summary>
    /// <param name="batch">The complete preflighted immutable batch and original request digest.</param>
    /// <param name="cancellationToken">Cancels preparation; durable uncertainty is returned explicitly.</param>
    Task<ReadModelBatchStagingResult> PrepareRecoveryManifestAsync(ReadModelBatch batch, CancellationToken cancellationToken = default);

    /// <summary>Loads only the exact original active discoverable preparation owned by this scope.</summary>
    /// <param name="scope">The original exact batch identity.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<ReadModelBatch?> ReadPreparedRecoveryBatchAsync(ReadModelBatchScope scope, CancellationToken cancellationToken = default);

    /// <summary>Reads content-free terminal evidence only after original private preparation has been reclaimed.</summary>
    /// <param name="scope">The original exact batch identity.</param>
    /// <param name="fingerprint">The immutable original manifest fingerprint.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<ReadModelBatchStagingResult> ReadRecoveryDecisionAsync(ReadModelBatchScope scope, string fingerprint, CancellationToken cancellationToken = default);

    /// <summary>Completes committed registered work and releases terminal registrations without retiring or committing prepared work.</summary>
    /// <param name="scope">The exact store, tenant, domain and aggregate; all projection registrations are inspected.</param>
    /// <param name="cancellationToken">Cancels settlement; uncertainty must prevent delivery acknowledgement.</param>
    Task<ReadModelPendingBatchRecoveryResult> SettleRecoveryScopeAsync(ReadModelBatchScope scope, CancellationToken cancellationToken = default);

    /// <summary>Rotates admission, retires registered preparations and removes their original private bytes.</summary>
    /// <param name="scope">The exact store, tenant, domain and aggregate. Projection and batch discriminators are ignored.</param>
    /// <param name="cancellationToken">Cancels recovery; unsuccessful recovery must retain the erasure obligation.</param>
    Task<ReadModelPendingBatchRecoveryResult> RetireRecoveryScopeAsync(ReadModelBatchScope scope, CancellationToken cancellationToken = default);
    /// <summary>Reopens registration only after the eraser has durably published its canonical anti-resurrection state.</summary>
    /// <param name="scope">The same exact aggregate scope.</param>
    /// <param name="cancellationToken">Cancels reopening; failure leaves admission closed for a safe retry.</param>
    Task ReleaseRecoveryScopeAsync(ReadModelBatchScope scope, CancellationToken cancellationToken = default);
}

