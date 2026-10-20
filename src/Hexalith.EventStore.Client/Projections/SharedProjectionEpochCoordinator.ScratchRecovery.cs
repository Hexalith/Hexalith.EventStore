namespace Hexalith.EventStore.Client.Projections;

public sealed partial class SharedProjectionEpochCoordinator
{
    /// <summary>Settles the original private batch before another scratch attempt or epoch CAS.</summary>
    private async Task<bool> ReconcilePendingScratchAsync(SharedProjectionScope scope, string key, CancellationToken token)
    {
        IReadModelPendingBatchRecoveryStore? recovery = _batches as IReadModelPendingBatchRecoveryStore
            ?? _store as IReadModelPendingBatchRecoveryStore;
        if (recovery is null) return false;
        ReadModelPendingBatchRecoveryResult result = await recovery.RecoverPendingAsync(
            new ReadModelBatchScope(scope.StoreName, scope.TenantId, scope.Domain, scope.Family, scope.Family,
                "scratch-recovery"), key, token).ConfigureAwait(false);
        if (!result.IsResolved)
            throw new SharedProjectionWriteUnavailableException(
                new InvalidOperationException("Private rebuild scratch recovery is incomplete: " + result.Reason));
        return result.WasRecovered;
    }
}
