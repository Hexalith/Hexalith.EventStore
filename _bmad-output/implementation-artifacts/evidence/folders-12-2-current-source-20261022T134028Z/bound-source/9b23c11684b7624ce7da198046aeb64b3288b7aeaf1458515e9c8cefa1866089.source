using System.Security.Cryptography;

using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>
/// Current anchor journal. Admit retains the original, record compares and advances, and recover and verify read that original.
/// A lost record stays Unknown until lookup. A missing store denies every call and writes no anchor.
/// </summary>
public sealed class ProtectionCurrentAnchorJournal : IAnchoredStateTransitionAuthority
{
    private readonly IProtectionAnchorStore? _store;

    /// <summary>Creates the journal. Production passes no store.</summary>
    /// <param name="store">Durable store. Null denies admit, record, recover, and verify.</param>
    public ProtectionCurrentAnchorJournal(IProtectionAnchorStore? store = null)
    {
        _store = store;
    }

    /// <summary>Confirms a previously advanced original. This lookup does not admit or advance.</summary>
    /// <param name="scopeId">Scope id.</param>
    /// <param name="expectedRevision">Predecessor revision.</param>
    /// <param name="targetRevision">Successor revision.</param>
    /// <param name="targetDigest">Exact target digest.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>True only when lookup shows that same advanced original.</returns>
    public async Task<bool> ConfirmsAsync(string scopeId, long expectedRevision, long targetRevision, string targetDigest, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_store is null || string.IsNullOrWhiteSpace(scopeId) || targetRevision != expectedRevision + 1 || !Hex(targetDigest))
        {
            return false;
        }

        try
        {
            ProtectionAnchorRecord? record = await _store.LookupScopeAsync(scopeId, cancellationToken).ConfigureAwait(false);
            return record is { Disposition: ProtectionAnchorDisposition.Advanced }
                && record.AnchorRevision == targetRevision
                && record.Transition.ScopeId == scopeId
                && record.Transition.ExpectedRevision == expectedRevision
                && record.Transition.TargetRevision == targetRevision
                && record.Transition.TargetDigest == targetDigest;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (transition is null || _store is null || !WellFormed(transition))
        {
            return false;
        }

        try
        {
            AnchoredStateTransition owned = Copy(transition);
            ProtectionAnchorDisposition disposition = await _store.AdmitAsync(owned, cancellationToken).ConfigureAwait(false);
            if (disposition != ProtectionAnchorDisposition.Admitted)
            {
                return false;
            }

            ProtectionAnchorRecord? record = await _store.ReadAsync(owned.ScopeId, cancellationToken).ConfigureAwait(false);
            return record is { Disposition: ProtectionAnchorDisposition.Admitted }
                && record.AnchorRevision == owned.ExpectedRevision
                && Exact(record.Transition, owned);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (transition is null || _store is null || !WellFormed(transition))
        {
            return false;
        }

        try
        {
            AnchoredStateTransition owned = Copy(transition);
            ProtectionAnchorDisposition disposition = await _store.RecoverAsync(owned, cancellationToken).ConfigureAwait(false);
            if (disposition is not (ProtectionAnchorDisposition.Admitted or ProtectionAnchorDisposition.Advanced))
            {
                return false;
            }

            ProtectionAnchorRecord? record = await _store.LookupAsync(owned, cancellationToken).ConfigureAwait(false);
            return record is not null && Exact(record.Transition, owned)
                && (disposition == ProtectionAnchorDisposition.Admitted
                    ? record.AnchorRevision == owned.ExpectedRevision
                    : record.AnchorRevision == owned.TargetRevision);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RecordTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (transition is null || _store is null || !WellFormed(transition))
        {
            return false;
        }

        try
        {
            AnchoredStateTransition owned = Copy(transition);
            ProtectionAnchorRecord? current = await _store.ReadAsync(owned.ScopeId, cancellationToken).ConfigureAwait(false);
            if (current is null || !Exact(current.Transition, owned))
            {
                return false;
            }

            if (current.Disposition == ProtectionAnchorDisposition.Advanced && current.AnchorRevision == owned.TargetRevision)
            {
                return true;
            }

            if (current.Disposition != ProtectionAnchorDisposition.Admitted || current.AnchorRevision != owned.ExpectedRevision)
            {
                return false;
            }

            ProtectionAnchorDisposition disposition = await _store.AdvanceAsync(owned, owned.ExpectedRevision, cancellationToken).ConfigureAwait(false);
            if (disposition != ProtectionAnchorDisposition.Advanced)
            {
                return false;
            }

            ProtectionAnchorRecord? recorded = await _store.ReadAsync(owned.ScopeId, cancellationToken).ConfigureAwait(false);
            return recorded is { Disposition: ProtectionAnchorDisposition.Advanced }
                && recorded.AnchorRevision == owned.TargetRevision
                && Exact(recorded.Transition, owned);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (transition is null || _store is null || !WellFormed(transition))
        {
            return false;
        }

        try
        {
            AnchoredStateTransition owned = Copy(transition);
            ProtectionAnchorRecord? record = await _store.LookupAsync(owned, cancellationToken).ConfigureAwait(false);
            return record is { Disposition: ProtectionAnchorDisposition.Advanced }
                && record.AnchorRevision == owned.TargetRevision
                && Exact(record.Transition, owned);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static AnchoredStateTransition Copy(AnchoredStateTransition transition)
        => transition with { TargetBytes = transition.TargetBytes.ToArray() };

    private static bool Exact(AnchoredStateTransition left, AnchoredStateTransition right)
        => left.ScopeId == right.ScopeId
            && left.ExpectedRevision == right.ExpectedRevision
            && left.TargetRevision == right.TargetRevision
            && left.PredecessorDigest == right.PredecessorDigest
            && left.TargetDigest == right.TargetDigest
            && left.TargetBytes.AsSpan().SequenceEqual(right.TargetBytes);

    private static bool WellFormed(AnchoredStateTransition? transition)
        => transition is not null
            && !string.IsNullOrWhiteSpace(transition.ScopeId)
            && transition.ScopeId.Length <= 4096
            && transition.ExpectedRevision >= 0
            && transition.TargetRevision == transition.ExpectedRevision + 1
            && transition.TargetBytes is { Length: > 0 and <= RecoverableAnchoredState.MaximumPendingBytes }
            && Hex(transition.PredecessorDigest)
            && Hex(transition.TargetDigest)
            && Convert.ToHexString(SHA256.HashData(transition.TargetBytes)) == transition.TargetDigest;

    private static bool Hex(string? value)
        => value is { Length: 64 } && value.All(static character => character is >= '0' and <= '9' or >= 'A' and <= 'F');
}
