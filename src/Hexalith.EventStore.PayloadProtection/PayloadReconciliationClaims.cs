// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; amendment section 4.
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
        ReconciliationV1 claimed = current.Value with { Attempt = checked(current.Value.Attempt + 1), Status = "Running", UpdatedAtUtc = PayloadProtectionStateCodec.Timestamp(_clock.GetUtcNow()) };
        PayloadStateCommitResult result = await store.CommitAsync([PayloadProtectionStateMutation.Put(claimed, current.ETag)], cancellationToken).ConfigureAwait(false);
        if (result == PayloadStateCommitResult.Conflict) { return null; }
        if (result != PayloadStateCommitResult.Committed) { throw new PayloadProtectionStateException(result == PayloadStateCommitResult.Denied ? UnreadableProtectedDataReason.ProviderDenied : UnreadableProtectedDataReason.ProviderUnavailable); }
        return await RequireAsync(claimed, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Refreshes metadata after an unchanged-value CAS without accepting a replacement claim.</summary>
    internal async ValueTask<PayloadProtectionStateValue<ReconciliationV1>> RequireAsync(ReconciliationV1 claim, CancellationToken cancellationToken = default)
    {
        PayloadProtectionStateValue<ReconciliationV1> current = await store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(claim.OperationId), cancellationToken).ConfigureAwait(false) ?? throw Mismatch();
        if (current.Value != claim || claim.Status != "Running") { throw Mismatch(); }
        return current;
    }
    private static PayloadProtectionStateException Mismatch() => new(UnreadableProtectedDataReason.ConsistencyMismatch);
}
