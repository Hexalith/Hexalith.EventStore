namespace Hexalith.EventStore.Client.Projections;

public sealed partial class SharedProjectionEpochCoordinator
{
    /// <summary>Replays the original retained private batch, including preparation without participants.</summary>
    internal async Task<bool> ReconcileScratchBatchAsync(SharedProjectionScope scope, string batchId, CancellationToken token)
        => (await ResolveScratchAttemptsAsync(scope, batchId, token).ConfigureAwait(false)).WasRecovered;

    private async Task<(string BatchId, bool WasRecovered)> ResolveScratchAttemptsAsync(
        SharedProjectionScope scope, string batchId, CancellationToken token)
    {
        IReadModelPendingBatchRecoveryStore? recovery = _batches as IReadModelPendingBatchRecoveryStore
            ?? _store as IReadModelPendingBatchRecoveryStore;
        if (recovery is null) return (batchId, false);
        IReadModelStore markerStore = _batches as IReadModelStore ?? _store;
        bool wasRecovered = false;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var batchScope = new ReadModelBatchScope(scope.StoreName, scope.TenantId, scope.Domain,
                scope.Family, scope.Family, batchId);
            ReadModelPendingBatchRecoveryResult result = await recovery.RecoverPendingAsync(batchScope,
                batchScope.ComputeMarkerKey(), token).ConfigureAwait(false);
            if (!result.IsResolved)
                throw new SharedProjectionWriteUnavailableException(new InvalidOperationException(
                    "Private rebuild scratch recovery is incomplete: " + result.Reason));
            wasRecovered |= result.WasRecovered;
            // Every link is an immutable, validated original terminal decision. Never reuse that
            // physical marker identity: a delayed installer can still hold its provider ETag.
            if (result.RetiredAttemptFingerprint is { } fingerprint)
            {
                await EraseTerminalBatchMarkerAsync(scope, batchId, token).ConfigureAwait(false);
                batchId = RetainedScratchRetryBatchId(batchScope, fingerprint);
                continue;
            }
            ReadModelBatchMarker? marker = (await markerStore.GetAsync<ReadModelBatchMarker>(scope.StoreName,
                batchScope.ComputeMarkerKey(), token).ConfigureAwait(false)).Value;
            if (marker is { Status: ReadModelBatchMarkerStatus.Aborted, ScratchCompensationKey: not null })
            {
                await EraseTerminalBatchMarkerAsync(scope, batchId, token).ConfigureAwait(false);
                batchId = RetainedLegacyScratchBatchId(batchScope);
                continue;
            }
            return (batchId, wasRecovered);
        }
    }

    /// <summary>Derives a bounded distinct attempt from the complete original immutable retirement.</summary>
    internal static string RetainedScratchRetryBatchId(ReadModelBatchScope originalScope, string fingerprint)
    {
        originalScope.Validate();
        if (!ReadModelBatchFingerprint.IsCanonical(fingerprint))
            throw new ArgumentException("Invalid private scratch retirement identity.", nameof(fingerprint));
        return "scratch-retry-v1:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(originalScope.ComputeScopeHash() + ":" + fingerprint)));
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
        => (await ResolveScratchAttemptsAsync(scope, batchId, token).ConfigureAwait(false)).BatchId;

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
