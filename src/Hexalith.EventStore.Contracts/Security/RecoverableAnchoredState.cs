using System.Security.Cryptography;
using System.Text.Json;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Shared technical ordering for bounded prospective bytes, independent exact transition proof and fresh durable reconciliation.</summary>
public static class RecoverableAnchoredState
{
    /// <summary>Gets the maximum serialized pending state; owning collection/carrier limits must additionally hold.</summary>
    public const int MaximumPendingBytes = 33554432;
    /// <summary>Captures one exact conditional transition before any independent advancement.</summary>
    public static AnchoredStateTransition Prepare<T>(string scope, long expected, long target, T predecessor, T next)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        if (scope.Length > 4096 || expected < 0 || target != checked(expected + 1)) { throw new ArgumentException("Invalid exact transition scope/revision."); }
        using var stream = new BoundedPendingStateStream(MaximumPendingBytes);
        JsonSerializer.Serialize(stream, next);
        byte[] bytes = stream.ToArray();
        if (bytes.Length > MaximumPendingBytes) { throw new InvalidOperationException("Pending state exceeds its bounded carrier."); }
        return new(scope, expected, target, Digest(predecessor), Convert.ToHexString(SHA256.HashData(bytes)), bytes);
    }

    /// <summary>Reconciles only an independently authenticated exact pending target over its original predecessor or already committed target.</summary>
    public static async Task<T> ReconcileAsync<T>(string scope, T current, AnchoredStateTransition? pending,
        Func<T, T> capture, Func<T, Task<bool>> validateCurrent, IAnchoredStateTransitionAuthority authority,
        Func<T, Task<T>> persistTarget, bool recoverAdmittedOriginal = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentNullException.ThrowIfNull(capture); ArgumentNullException.ThrowIfNull(validateCurrent);
        ArgumentNullException.ThrowIfNull(authority); ArgumentNullException.ThrowIfNull(persistTarget);
        if (pending is not null)
        {
            Validate(pending, scope);
            // Detach before any external await; proof is over the exact owned bytes and both comparison digests.
            var owned = pending with { TargetBytes = pending.TargetBytes.ToArray() };
            T prospective = capture(JsonSerializer.Deserialize<T>(owned.TargetBytes)!);
            if (Digest(prospective) != owned.TargetDigest) { throw new InvalidOperationException("Pending state is not its canonical captured target."); }
            string currentDigest = Digest(current);
            bool verified = await authority.VerifyTransitionAsync(owned).ConfigureAwait(false);
            // Ordinary reads cannot elevate a primary-store candidate. A separately authorized mutation may ask
            // the independent owner to resolve only its already retained exact admitted original.
            if (!verified && recoverAdmittedOriginal && currentDigest == owned.PredecessorDigest
                && await validateCurrent(current).ConfigureAwait(false)
                && await authority.RecoverTransitionAsync(owned).ConfigureAwait(false))
            { verified = await authority.VerifyTransitionAsync(owned).ConfigureAwait(false); }
            if (verified && await validateCurrent(prospective).ConfigureAwait(false))
            {
                if (currentDigest != owned.PredecessorDigest && currentDigest != owned.TargetDigest)
                { throw new InvalidOperationException("Pending transition cannot overwrite a different original state."); }
                T durable = capture(await persistTarget(prospective).ConfigureAwait(false));
                if (Digest(durable) != owned.TargetDigest || !await authority.VerifyTransitionAsync(owned).ConfigureAwait(false)
                    || !await validateCurrent(durable).ConfigureAwait(false))
                { throw new InvalidOperationException("Reconciled target is not freshly durable under current exact authority."); }
                return durable;
            }
        }
        if (!await validateCurrent(current).ConfigureAwait(false)) { throw new InvalidOperationException("Independent exact state anchor is absent or divergent."); }
        return current;
    }

    /// <summary>Persists and freshly confirms pending bytes before atomically advancing the independent transition journal.</summary>
    public static async Task<bool> CommitAsync(AnchoredStateTransition transition, IAnchoredStateTransitionAuthority authority,
        Func<Task<AnchoredStateTransition?>> readPending, Func<AnchoredStateTransition, Task> persistPending)
    {
        ArgumentNullException.ThrowIfNull(transition); ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(readPending); ArgumentNullException.ThrowIfNull(persistPending);
        // Keep the caller's mutable carrier outside the conditional transition protocol.
        var owned = Copy(transition);
        Validate(owned, owned.ScopeId);
        var original = await readPending().ConfigureAwait(false);
        if (original is not null)
        {
            if (!Exact(original, owned))
            { throw new InvalidOperationException("A different original pending transition is retained."); }
        }
        // Each provider borrows a separate snapshot. An adapter retaining it after its Task
        // completes must first establish independent storage ownership.
        if (!await authority.AdmitTransitionAsync(Copy(owned)).ConfigureAwait(false)) { return false; }
        await persistPending(Copy(owned)).ConfigureAwait(false);
        var confirmed = await readPending().ConfigureAwait(false);
        if (confirmed is null || !Exact(confirmed, owned)) { throw new InvalidOperationException("Pending transition was not confirmed durable."); }
        return await authority.RecordTransitionAsync(Copy(owned)).ConfigureAwait(false);
    }

    /// <summary>Computes the exact canonical state fingerprint used by the independently installed owner.</summary>
    public static string Digest<T>(T value)
    {
        using var stream = new BoundedPendingStateStream(MaximumPendingBytes);
        JsonSerializer.Serialize(stream, value); return Convert.ToHexString(SHA256.HashData(stream.GetBuffer().AsSpan(0, checked((int)stream.Length))));
    }
    private static AnchoredStateTransition Copy(AnchoredStateTransition value) => value with { TargetBytes = value.TargetBytes.ToArray() };
    private static bool Exact(AnchoredStateTransition a, AnchoredStateTransition b) => a.ScopeId == b.ScopeId && a.ExpectedRevision == b.ExpectedRevision
        && a.TargetRevision == b.TargetRevision && a.PredecessorDigest == b.PredecessorDigest && a.TargetDigest == b.TargetDigest && a.TargetBytes.AsSpan().SequenceEqual(b.TargetBytes);
    private static void Validate(AnchoredStateTransition transition, string scope)
    {
        if (transition.ScopeId != scope || transition.ExpectedRevision < 0 || transition.TargetRevision != checked(transition.ExpectedRevision + 1)
            || transition.TargetBytes is not { Length: > 0 and <= MaximumPendingBytes }
            || !Hex(transition.PredecessorDigest) || !Hex(transition.TargetDigest)
            || Convert.ToHexString(SHA256.HashData(transition.TargetBytes)) != transition.TargetDigest)
        { throw new InvalidOperationException("Malformed exact pending transition."); }
    }
    private static bool Hex(string s) => s is { Length: 64 } && s.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
}
