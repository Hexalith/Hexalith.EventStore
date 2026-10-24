// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base section 10.2/PF-02; amendment section 7.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Proves create-only, stale/unchanged-value CAS, atomic rollback and TTL through the actual Dapr primitive.</summary>
/// <remarks>This startup probe is necessary capability evidence; full fault/concurrency PF-02 qualification remains a separate gate.</remarks>
internal sealed class PayloadProtectionCapabilityProbe(DaprPayloadProtectionStateStore store)
{
    /// <summary>Runs isolated exact-key probes and publishes readiness only after persisted assertions succeed.</summary>
    internal async ValueTask<string> ProbeAsync(CancellationToken cancellationToken = default)
    {
        store.RevokeQualification();
        string component = await store.InspectCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
        string probe = new CryptographicPayloadProtectionEntropy().CreateKeyReference();
        string root = PayloadProtectionStateKeys.Digest(probe);
        string otherRoot = PayloadProtectionStateKeys.Digest(probe + "-rollback");
        string timestamp = PayloadProtectionStateCodec.Timestamp(DateTimeOffset.UtcNow);
        var value = new WorkHeadV1(root, 0, 0, 0, timestamp);
        var other = new WorkHeadV1(otherRoot, 0, 0, 0, timestamp);
        string key = PayloadProtectionStateCodec.KeyFor(value);
        string otherKey = PayloadProtectionStateCodec.KeyFor(other);
        string ttlKey = PayloadProtectionStateKeys.MemberHead(root);
        try
        {
            Require(await store.CommitQualifiedAsync([PayloadProtectionStateMutation.Put(value)], cancellationToken).ConfigureAwait(false) == PayloadStateCommitResult.Committed);
            PayloadProtectionStateValue<WorkHeadV1> initial = await ReadAsync(key, cancellationToken).ConfigureAwait(false);
            // A rejected conditional creation must roll back the companion insertion, regardless of its position.
            _ = await store.CommitQualifiedAsync([PayloadProtectionStateMutation.Put(other), PayloadProtectionStateMutation.Put(value with { Revision = 1 })], cancellationToken).ConfigureAwait(false);
            Require(await store.ReadQualifiedAsync<WorkHeadV1>(otherKey, cancellationToken).ConfigureAwait(false) is null);
            Require((await ReadAsync(key, cancellationToken).ConfigureAwait(false)).Value.Revision == 0);

            Require(await store.CommitQualifiedAsync([PayloadProtectionStateMutation.Compare(initial)], cancellationToken).ConfigureAwait(false) == PayloadStateCommitResult.Committed);
            PayloadProtectionStateValue<WorkHeadV1> compared = await ReadAsync(key, cancellationToken).ConfigureAwait(false);
            Require(compared.Bytes.Span.SequenceEqual(initial.Bytes.Span) && compared.ETag != initial.ETag);
            _ = await store.CommitQualifiedAsync([PayloadProtectionStateMutation.Put(other), PayloadProtectionStateMutation.Put(value with { Revision = 2 }, initial.ETag)], cancellationToken).ConfigureAwait(false);
            Require(await store.ReadQualifiedAsync<WorkHeadV1>(otherKey, cancellationToken).ConfigureAwait(false) is null);
            Require((await ReadAsync(key, cancellationToken).ConfigureAwait(false)).Value.Revision == 0);

            Require(await store.CommitQualifiedAsync([PayloadProtectionStateMutation.Put(value with { Revision = 3 }, compared.ETag), PayloadProtectionStateMutation.Put(other)], cancellationToken).ConfigureAwait(false) == PayloadStateCommitResult.Committed);
            Require((await ReadAsync(key, cancellationToken).ConfigureAwait(false)).Value.Revision == 3);
            Require((await ReadAsync(otherKey, cancellationToken).ConfigureAwait(false)).Value.Revision == 0);

            // A short-lived isolated probe uses the same transaction metadata path as actual 45/60-second leases.
            byte[] ttlValue = PayloadProtectionStateCodec.Encode(new MemberHeadV1(root, 0, 0, 0, timestamp));
            Require(await store.CommitQualifiedAsync([new(ttlKey, ttlValue, null, 1)], cancellationToken).ConfigureAwait(false) == PayloadStateCommitResult.Committed);
            Require(await store.ReadQualifiedAsync<MemberHeadV1>(ttlKey, cancellationToken).ConfigureAwait(false) is not null);
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            Require(await store.ReadQualifiedAsync<MemberHeadV1>(ttlKey, cancellationToken).ConfigureAwait(false) is null);
            cancellationToken.ThrowIfCancellationRequested();
            store.ConfirmQualification();
            return component;
        }
        finally
        {
            // Only exact random probe keys are touched; cleanup failure never supplies capability evidence.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await CleanupAsync<WorkHeadV1>(key, cleanup.Token).ConfigureAwait(false);
            await CleanupAsync<WorkHeadV1>(otherKey, cleanup.Token).ConfigureAwait(false);
            await CleanupAsync<MemberHeadV1>(ttlKey, cleanup.Token).ConfigureAwait(false);
        }
    }

    private async ValueTask<PayloadProtectionStateValue<WorkHeadV1>> ReadAsync(string key, CancellationToken cancellationToken)
        => await store.ReadQualifiedAsync<WorkHeadV1>(key, cancellationToken).ConfigureAwait(false)
            ?? throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ConsistencyMismatch);

    private async ValueTask CleanupAsync<T>(string key, CancellationToken cancellationToken) where T : class
    {
        try
        {
            PayloadProtectionStateValue<T>? current = await store.ReadQualifiedAsync<T>(key, cancellationToken).ConfigureAwait(false);
            if (current is not null)
            {
                _ = await store.CommitQualifiedAsync([new(key, null, current.ETag)], cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            // Probe keys contain no personal data/key material, and are never reused as authority.
        }
    }

    private static void Require(bool condition)
    {
        if (!condition) { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ConsistencyMismatch); }
    }
}
