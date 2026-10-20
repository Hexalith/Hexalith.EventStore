namespace Hexalith.EventStore.Client.Projections;

public sealed partial class SharedProjectionEpochCoordinator
{
    /// <summary>Replays the original retained private batch, including preparation without participants.</summary>
    internal async Task<bool> ReconcileScratchBatchAsync(SharedProjectionScope scope, string batchId, CancellationToken token)
    {
        var batchScope = new ReadModelBatchScope(scope.StoreName, scope.TenantId, scope.Domain,
            scope.Family, scope.Family, batchId);
        IReadModelPendingBatchRecoveryStore? recovery = _batches as IReadModelPendingBatchRecoveryStore
            ?? _store as IReadModelPendingBatchRecoveryStore;
        if (recovery is null) return false;
        ReadModelPendingBatchRecoveryResult result = await recovery.RecoverPendingAsync(batchScope,
            batchScope.ComputeMarkerKey(), token).ConfigureAwait(false);
        if (!result.IsResolved)
            throw new SharedProjectionWriteUnavailableException(new InvalidOperationException(
                "Private rebuild scratch recovery is incomplete: " + result.Reason));
        // Keep the original legacy terminal marker to prevent provider ETag ABA. Retained
        // replacement work uses a separate deterministic SDK identity, discovered here as well.
        IReadModelStore markerStore = _batches as IReadModelStore ?? _store;
        ReadModelBatchMarker? marker = (await markerStore.GetAsync<ReadModelBatchMarker>(scope.StoreName,
            batchScope.ComputeMarkerKey(), token).ConfigureAwait(false)).Value;
        if (marker is { Status: ReadModelBatchMarkerStatus.Aborted, ScratchCompensationKey: not null })
        {
            await EraseTerminalBatchMarkerAsync(scope, batchId, token).ConfigureAwait(false);
            ReadModelBatchScope retainedScope = batchScope with { BatchId = RetainedLegacyScratchBatchId(batchScope) };
            ReadModelPendingBatchRecoveryResult retained = await recovery.RecoverPendingAsync(retainedScope,
                retainedScope.ComputeMarkerKey(), token).ConfigureAwait(false);
            if (!retained.IsResolved)
                throw new SharedProjectionWriteUnavailableException(new InvalidOperationException(
                    "Private rebuild scratch recovery is incomplete: " + retained.Reason));
            return result.WasRecovered || retained.WasRecovered;
        }
        return result.WasRecovered;
    }

    /// <summary>Derives a distinct bounded replacement identity without extending public component limits.</summary>
    /// <param name="originalScope">The original validated private identity.</param>
    /// <returns>A deterministic SDK prefix and digest of the complete original scope.</returns>
    internal static string RetainedLegacyScratchBatchId(ReadModelBatchScope originalScope)
    {
        originalScope.Validate();
        string prefix = originalScope.BatchId.StartsWith("scratch-retained-v1:", StringComparison.Ordinal)
            ? "scratch-resume-v1:" : "scratch-retained-v1:";
        return prefix + originalScope.ComputeScopeHash();
    }

    private async Task<string> ScratchBatchIdentityAsync(SharedProjectionScope scope, string batchId, CancellationToken token)
    {
        var batchScope = new ReadModelBatchScope(scope.StoreName, scope.TenantId, scope.Domain, scope.Family, scope.Family, batchId);
        IReadModelStore markerStore = _batches as IReadModelStore ?? _store;
        ReadModelBatchMarker? marker = (await markerStore.GetAsync<ReadModelBatchMarker>(scope.StoreName,
            batchScope.ComputeMarkerKey(), token).ConfigureAwait(false)).Value;
        return marker is { Status: ReadModelBatchMarkerStatus.Aborted, ScratchCompensationKey: not null }
            ? RetainedLegacyScratchBatchId(batchScope) : batchId;
    }

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
