// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; amendment section 4; approved review correction section 8.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Acquires increasing ETag-fenced claims without interpreting elapsed time as persistence evidence.</summary>
internal sealed class PayloadReconciliationClaims(IPayloadProtectionStateStore store, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    /// <summary>Claims exact indexed work; concurrent claims fence every resumed predecessor.</summary>
    internal async ValueTask<PayloadProtectionStateValue<ReconciliationV1>?> TryClaimAsync(WorkEntryV1 entry, CancellationToken cancellationToken = default)
    {
        PayloadProtectionStateValue<ReconciliationV1> current = await store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(entry.OperationId), cancellationToken).ConfigureAwait(false) ?? throw Mismatch();
        if (current.Value.ScopeDigest != entry.ScopeDigest || current.Value.OperationId != entry.OperationId
            || current.Value.Status is "Completed" or "Quarantined") { throw Mismatch(); }
        if (PayloadProtectionStateCodec.ParseTimestamp(current.Value.NotBeforeUtc) > _clock.GetUtcNow()) { return null; }
        ReconciliationV1 claimed = current.Value with { Attempt = checked(current.Value.Attempt + 1), Status = "Running", NotBeforeUtc = PayloadProtectionStateCodec.Timestamp(_clock.GetUtcNow().AddSeconds(90)), UpdatedAtUtc = PayloadProtectionStateCodec.Timestamp(_clock.GetUtcNow()) };
        PayloadStateCommitResult result = await store.CommitAsync([PayloadProtectionStateMutation.Put(claimed, current.ETag)], cancellationToken).ConfigureAwait(false);
        if (result == PayloadStateCommitResult.Conflict) { return null; }
        if (result != PayloadStateCommitResult.Committed) { throw new PayloadProtectionStateException(result == PayloadStateCommitResult.Denied ? UnreadableProtectedDataReason.ProviderDenied : UnreadableProtectedDataReason.ProviderUnavailable); }
        return await RequireAsync(claimed, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Refreshes metadata after an unchanged-value CAS without accepting a replacement claim.</summary>
    internal async ValueTask<PayloadProtectionStateValue<ReconciliationV1>> RequireAsync(ReconciliationV1 claim, CancellationToken cancellationToken = default, bool foreground = false)
    {
        PayloadProtectionStateValue<ReconciliationV1> current = await store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(claim.OperationId), cancellationToken).ConfigureAwait(false) ?? throw Mismatch();
        if (current.Value != claim || (claim.Status != "Running" && !(foreground && claim.Status == "Pending" && claim.Attempt == 0))
            || PayloadProtectionStateCodec.ParseTimestamp(claim.NotBeforeUtc) <= _clock.GetUtcNow()) { throw Mismatch(); }
        return current;
    }
    private static PayloadProtectionStateException Mismatch() => new(UnreadableProtectedDataReason.ConsistencyMismatch);
}
