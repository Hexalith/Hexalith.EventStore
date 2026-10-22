using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>
/// Fail-closed online consumption authority. It verifies one published detached ES256 entry, then rereads the guard.
/// It does not cache that read, self-issue an anchor, or authorize a physical effect.
/// </summary>
public sealed class PublishedTrustDeletionConsumptionAuthority : IDeletionConsumptionAuthority
{
    private const string KeyFamily = "DeletionBatchCapabilitySigningKey";
    private const string AdmissionBlockedOutcome = "AdmissionBlocked";
    private const string CurveOid = "1.2.840.10045.3.1.7";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly PublishedDeletionCapabilityTrust[] _publishedTrust;
    private readonly IGovernanceScopeGuard _guard;
    private readonly IAnchoredStateTransitionAuthority? _journal;
    private readonly IPublishedTrustRevocationEvidence? _revocations;

    /// <summary>Creates the authority from injected published trust and a guard that is reread on every check.</summary>
    /// <param name="publishedTrust">Published trust entries. Exactly one unrevoked match may verify a capability.</param>
    /// <param name="guard">Fresh guard reader. Each dispatch check reads it twice and keeps neither result.</param>
    /// <param name="journal">Independent transition journal. Null denies operation, state, revision, and the four transition methods. This authority does not mint anchors.</param>
    /// <param name="revocations">Injected revocation evidence. Null denies revocation and activation.</param>
    public PublishedTrustDeletionConsumptionAuthority(
        IReadOnlyList<PublishedDeletionCapabilityTrust> publishedTrust,
        IGovernanceScopeGuard guard,
        IAnchoredStateTransitionAuthority? journal = null,
        IPublishedTrustRevocationEvidence? revocations = null)
    {
        ArgumentNullException.ThrowIfNull(publishedTrust);
        ArgumentNullException.ThrowIfNull(guard);
        if (publishedTrust.Any(static entry => entry is null))
        {
            throw new ArgumentException("Published trust contains an absent entry.", nameof(publishedTrust));
        }

        _publishedTrust = publishedTrust.ToArray();
        _guard = guard;
        _journal = journal;
        _revocations = revocations;
    }

    /// <inheritdoc />
    public async Task<bool> AuthorizeOperationAsync(string tenantId, string identity, string operation, CancellationToken cancellationToken = default)
    {
        if (_journal is null || !Text(tenantId) || !Text(identity) || !Text(operation))
        {
            return false;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            TenantGovernanceGuardState? state = await _guard.ReadAsync(tenantId, cancellationToken).ConfigureAwait(false);
            if (state is null || state.TenantId != tenantId || state.Revision < 1)
            {
                return false;
            }

            return await _journal.VerifyTransitionAsync(Proof("authorize", tenantId, identity, state.Revision, operation), cancellationToken).ConfigureAwait(false);
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
    public async Task<bool> ValidateStateAsync(string tenantId, long revision, string exactStateDigest, CancellationToken cancellationToken = default)
    {
        if (_journal is null || !Text(tenantId) || revision < 1 || !Hex(exactStateDigest))
        {
            return false;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            TenantGovernanceGuardState? state = await _guard.ReadAsync(tenantId, cancellationToken).ConfigureAwait(false);
            if (state is null || state.TenantId != tenantId || state.Revision != revision)
            {
                return false;
            }

            return await _journal.VerifyTransitionAsync(Proof("state", tenantId, "state", revision, exactStateDigest), cancellationToken).ConfigureAwait(false);
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
    public async Task<bool> RecordRevisionAsync(string tenantId, long expectedRevision, long nextRevision, string exactStateDigest, CancellationToken cancellationToken = default)
    {
        if (_journal is null || !Text(tenantId) || nextRevision < 1 || nextRevision != expectedRevision + 1 || !Hex(exactStateDigest))
        {
            return false;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            TenantGovernanceGuardState? state = await _guard.ReadAsync(tenantId, cancellationToken).ConfigureAwait(false);
            if (state is null || state.TenantId != tenantId || state.Revision != expectedRevision && state.Revision != nextRevision)
            {
                return false;
            }

            return await _journal.VerifyTransitionAsync(Proof("revision", tenantId, "revision", nextRevision, exactStateDigest), cancellationToken).ConfigureAwait(false);
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
    public async Task<bool> VerifyDispatchAsync(DeletionBatchConsumptionRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request is null)
            {
                return false;
            }

            DeletionBatchConsumptionRequest captured = DeletionConsumptionIdentity.Capture(request);
            if (!DeletionConsumptionIdentity.Same(captured, request) || !SignatureMatches(captured))
            {
                return false;
            }

            TenantGovernanceGuardState? first = await _guard.ReadAsync(captured.Capability.TenantId, cancellationToken).ConfigureAwait(false);
            byte[]? firstBytes = first is null ? null : JsonSerializer.SerializeToUtf8Bytes(first);
            TenantGovernanceGuardState? second = await _guard.ReadAsync(captured.Capability.TenantId, cancellationToken).ConfigureAwait(false);
            byte[]? secondBytes = second is null ? null : JsonSerializer.SerializeToUtf8Bytes(second);
            if (firstBytes is null || secondBytes is null || !firstBytes.AsSpan().SequenceEqual(secondBytes))
            {
                return false;
            }

            // Admit only the compared bytes, never collections still owned by the guard reader.
            TenantGovernanceGuardState? snapshot = JsonSerializer.Deserialize<TenantGovernanceGuardState>(secondBytes);
            return snapshot is not null && Admitted(snapshot, captured);
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
    public async Task<bool> VerifyAdmissionBlockAsync(DeletionBatchBlockRequest request, CancellationToken cancellationToken = default)
    {
        if (_journal is null || request is null || !Text(request.TenantId) || !Text(request.BatchId) || !Text(request.OperationId) || !Text(request.AdmissionEvidenceId))
        {
            return false;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            TenantGovernanceGuardState? state = await _guard.ReadAsync(request.TenantId, cancellationToken).ConfigureAwait(false);
            if (state is null || state.TenantId != request.TenantId || SingleBatch(state, request.BatchId) is null
                || !HasCommittedReceipt(state, request.AdmissionEvidenceId, request.BatchId))
            {
                return false;
            }

            return await _journal.VerifyTransitionAsync(Proof("admission-block", request.TenantId, request.BatchId, state.Revision, request.AdmissionEvidenceId), cancellationToken).ConfigureAwait(false);
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
    public async Task<bool> VerifyRevocationAsync(DeletionCapabilityRevocationEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (_journal is null || _revocations is null || envelope is null || envelope.KeyFamily != KeyFamily || envelope.RevocationRevision < 1
            || envelope.TrustProfileRevision < 1 || !Text(envelope.TenantId) || !Text(envelope.Issuer) || !Text(envelope.Audience)
            || !Text(envelope.KeyVersion) || !Text(envelope.EventIdentity) || !Hex(envelope.SignatureDigest)
            || !_revocations.Matches(envelope) || _revocations.Pending(envelope) || !SoleTrust(envelope.Issuer, envelope.Audience, envelope.TenantId, envelope.KeyVersion))
        {
            return false;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            TenantGovernanceGuardState? state = await _guard.ReadAsync(envelope.TenantId, cancellationToken).ConfigureAwait(false);
            if (state is null || state.TenantId != envelope.TenantId)
            {
                return false;
            }

            return await _journal.VerifyTransitionAsync(Proof("revocation", envelope.TenantId, envelope.EventIdentity, state.Revision, envelope.SignatureDigest), cancellationToken).ConfigureAwait(false);
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
    public async Task<bool> VerifyActivationAsync(DeletionReattestationActivation activation, CancellationToken cancellationToken = default)
    {
        if (_journal is null || _revocations is null || activation?.Replacement?.Capability is null || activation.ExpectedKeyBlockSetRevision < 0
            || !Text(activation.OperationId) || !Text(activation.CompromiseBlockReceiptId) || !Text(activation.GuardReplacementReceiptId)
            || !_revocations.MatchesActivation(activation) || _revocations.PendingActivation(activation))
        {
            return false;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeletionBatchCapabilityV1 capability = activation.Replacement.Capability;
            if (!SoleTrust(capability))
            {
                return false;
            }

            TenantGovernanceGuardState? state = await _guard.ReadAsync(capability.TenantId, cancellationToken).ConfigureAwait(false);
            GovernanceBatchState? batch = state is null || state.TenantId != capability.TenantId ? null : SingleBatch(state, capability.BatchId);
            string dispatchReceiptId = activation.Replacement.DispatchReceiptId;
            long nextAttestation = batch is null ? 0 : checked(batch.AttestationOrdinal + 1);
            if (state is null || batch is null || state.Revision < 1 || batch.ProtectionOutcome == AdmissionBlockedOutcome
                || batch.ManifestDigest != capability.ManifestDigest || capability.TenantId != state.TenantId
                || capability.CapabilityKeyVersion != batch.CapabilityKeyVersion
                || activation.ExpectedKeyBlockSetRevision != batch.BlockSetRevision
                || dispatchReceiptId != batch.DispatchReceiptId
                || state.Revision < activation.Replacement.DispatchGuardRevision
                || capability.AttestationOrdinal != batch.AttestationOrdinal && capability.AttestationOrdinal != nextAttestation
                || activation.CompromiseBlockReceiptId == activation.GuardReplacementReceiptId
                || activation.CompromiseBlockReceiptId == dispatchReceiptId
                || activation.GuardReplacementReceiptId == dispatchReceiptId
                || !HasCommittedReceipt(state, activation.CompromiseBlockReceiptId, capability.BatchId)
                || !HasCommittedReceipt(state, activation.GuardReplacementReceiptId, capability.BatchId)
                || !HasCommittedReceipt(state, dispatchReceiptId, capability.BatchId))
            {
                return false;
            }

            return await _journal.VerifyTransitionAsync(Proof("activation", capability.TenantId, capability.BatchId, state.Revision, activation.OperationId), cancellationToken).ConfigureAwait(false);
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
    public Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => DelegateTransition(transition, static (journal, value, token) => journal.AdmitTransitionAsync(value, token), cancellationToken);

    /// <inheritdoc />
    public Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => DelegateTransition(transition, static (journal, value, token) => journal.RecoverTransitionAsync(value, token), cancellationToken);

    /// <inheritdoc />
    public Task<bool> RecordTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => DelegateTransition(transition, static (journal, value, token) => journal.RecordTransitionAsync(value, token), cancellationToken);

    /// <inheritdoc />
    public Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => DelegateTransition(transition, static (journal, value, token) => journal.VerifyTransitionAsync(value, token), cancellationToken);

    private Task<bool> DelegateTransition(
        AnchoredStateTransition transition,
        Func<IAnchoredStateTransitionAuthority, AnchoredStateTransition, CancellationToken, Task<bool>> call,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_journal is null || transition is null)
        {
            return Task.FromResult(false);
        }

        return InvokeTransition(call, transition, cancellationToken);
    }

    private async Task<bool> InvokeTransition(
        Func<IAnchoredStateTransitionAuthority, AnchoredStateTransition, CancellationToken, Task<bool>> call,
        AnchoredStateTransition transition,
        CancellationToken cancellationToken)
    {
        try
        {
            return await call(_journal!, transition, cancellationToken).ConfigureAwait(false);
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

    private static AnchoredStateTransition Proof(string kind, string tenantId, string identity, long revision, string material)
    {
        if (revision < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(revision));
        }

        long expected = checked(revision - 1);
        long targetRevision = checked(expected + 1);
        byte[] bytes = StrictUtf8.GetBytes(string.Join('\n', kind, tenantId, identity, revision.ToString(CultureInfo.InvariantCulture), material));
        string target = Convert.ToHexString(SHA256.HashData(bytes));
        string predecessor = Convert.ToHexString(SHA256.HashData(StrictUtf8.GetBytes(kind + "|predecessor|" + tenantId + "|" + expected.ToString(CultureInfo.InvariantCulture))));
        return new("published-trust|" + kind + "|" + tenantId + "|" + identity, expected, targetRevision, predecessor, target, bytes);
    }

    private bool SoleTrust(DeletionBatchCapabilityV1 capability)
        => SoleTrust(capability.Issuer, capability.Audience, capability.TenantId, capability.CapabilityKeyVersion);

    private bool SoleTrust(string issuer, string audience, string tenantId, string keyVersion)
    {
        int matches = _publishedTrust.Count(entry => entry is not null
            && !entry.IsRevoked
            && entry.TrustProfileRevision > 0
            && entry.KeyFamily == KeyFamily
            && entry.Issuer == issuer
            && entry.Audience == audience
            && entry.TenantId == tenantId
            && entry.CapabilityKeyVersion == keyVersion
            && !string.IsNullOrWhiteSpace(entry.PublicAnchorId)
            && !string.IsNullOrWhiteSpace(entry.PublicAnchorVersion));
        return matches == 1;
    }

    private static GovernanceBatchState? SingleBatch(TenantGovernanceGuardState state, string batchId)
    {
        if (state.Deletions is null)
        {
            return null;
        }

        GovernanceBatchState[] batches = state.Deletions.Where(static deletion => deletion?.Batches is not null)
            .SelectMany(deletion => deletion.Batches.Where(batch => batch is not null && batch.BatchId == batchId))
            .ToArray();
        if (batches.Length != 1)
        {
            return null;
        }

        GovernanceDeletionState? owner = state.Deletions.SingleOrDefault(deletion => deletion?.Batches?.Any(batch => batch?.BatchId == batchId) == true);
        return owner?.Scope is not null && owner.Scope.TenantId == state.TenantId ? batches[0] : null;
    }

    private static bool HasCommittedReceipt(TenantGovernanceGuardState state, string receiptId, string referenceId)
    {
        if (state.Receipts is null || string.IsNullOrWhiteSpace(receiptId))
        {
            return false;
        }

        GovernanceProtocolReceipt[] found = state.Receipts.Where(receipt => receipt is not null && receipt.ReceiptId == receiptId && receipt.ReferenceId == referenceId).ToArray();
        return found.Length == 1 && found[0].Status == "Committed";
    }

    private static bool Hex(string? value)
        => value is { Length: 64 } && value.All(static character => character is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f');

    private bool SignatureMatches(DeletionBatchConsumptionRequest request)
    {
        DeletionBatchCapabilityV1 capability = request.Capability;
        if (capability is null)
        {
            return false;
        }

        PublishedDeletionCapabilityTrust[] matches = _publishedTrust.Where(entry => entry is not null
            && !entry.IsRevoked
            && entry.TrustProfileRevision > 0
            && entry.KeyFamily == KeyFamily
            && entry.Issuer == capability.Issuer
            && entry.Audience == capability.Audience
            && entry.TenantId == capability.TenantId
            && entry.CapabilityKeyVersion == capability.CapabilityKeyVersion
            && !string.IsNullOrWhiteSpace(entry.PublicAnchorId)
            && !string.IsNullOrWhiteSpace(entry.PublicAnchorVersion)
            && entry.SubjectPublicKeyInfo is { Length: > 0 }).ToArray();
        if (matches.Length != 1)
        {
            return false;
        }

        byte[] payload = DeletionBatchCapabilityIdentity.CanonicalPayload(capability);
        string digest = DeletionBatchCapabilityIdentity.TargetManifestDigest(request.Targets);
        if (!string.Equals(digest, capability.ManifestDigest, StringComparison.Ordinal))
        {
            return false;
        }

        return Verify(Header(matches[0]), payload, request.DetachedJws, matches[0].SubjectPublicKeyInfo);
    }

    private static bool Admitted(TenantGovernanceGuardState state, DeletionBatchConsumptionRequest request)
    {
        DeletionBatchCapabilityV1 capability = request.Capability;
        if (state.TenantId != capability.TenantId || state.Revision < request.DispatchGuardRevision
            || state.Deletions is null || state.Receipts is null || state.Holds is null || state.CompromisedKeyVersions is null
            || state.Holds.Any(hold => !ValidHold(hold, state.TenantId))
            || state.Holds.Select(hold => hold.HoldId).Distinct(StringComparer.Ordinal).Count() != state.Holds.Count)
        {
            return false;
        }

        GovernanceBatchState[] batches = state.Deletions.Where(deletion => deletion is not null && deletion.Batches is not null)
            .SelectMany(deletion => deletion.Batches.Where(batch => batch is not null && batch.BatchId == capability.BatchId)
                .Select(batch => batch))
            .ToArray();
        if (batches.Length != 1)
        {
            return false;
        }

        GovernanceDeletionState? owner = state.Deletions.SingleOrDefault(deletion => deletion?.Batches?.Any(batch => batch?.BatchId == capability.BatchId) == true);
        GovernanceBatchState batch = batches[0];
        string jwsDigest = Convert.ToHexString(SHA256.HashData(StrictUtf8.GetBytes(request.DetachedJws)));
        string signingId = DeletionBatchCapabilityIdentity.SigningRequestId(capability);
        if (owner is null || !ValidScope(owner.Scope, state.TenantId)
            || owner.IntegrityCompromised || owner.RequestId != capability.DeletionRequestId || owner.SealId != capability.DestructionSealId
            || batch.Capability != capability || batch.DetachedJws != request.DetachedJws || batch.SigningRequestId != signingId
            || batch.Kind != capability.BatchKind || batch.Ordinal != capability.BatchOrdinal || batch.ManifestDigest != capability.ManifestDigest
            || batch.AttestationOrdinal != capability.AttestationOrdinal || batch.CapabilityKeyVersion != capability.CapabilityKeyVersion
            || !string.Equals(batch.AttestationDigest, jwsDigest, StringComparison.Ordinal)
            || batch.IssuedGuardRevision != request.CommittedIssuedGuardRevision || batch.DispatchGuardRevision != request.DispatchGuardRevision
            || request.CommittedIssuedGuardRevision != checked(capability.IntendedIssuedGuardRevision + 1)
            || string.IsNullOrWhiteSpace(batch.IssueReceiptId) || batch.IssueReceiptId == batch.DispatchReceiptId
            || batch.DispatchReceiptId != request.DispatchReceiptId || batch.ProtectionOutcome != "Unconsumed"
            || state.CompromisedKeyVersions.Contains(batch.CapabilityKeyVersion, StringComparer.Ordinal)
            || !SameTargets(batch.Targets, request.Targets)
            || batch.DispatchGuardRevision <= batch.IssuedGuardRevision)
        {
            return false;
        }

        // Each batch holds its current attestation in place. The global exact-BatchId
        // lookup above rejects duplicate credentials; other containment batches may
        // independently have their own dispatched current attestation.
        return Committed(state, batch.IssueReceiptId, batch.IssuedGuardRevision, capability, signingId, jwsDigest)
            && Committed(state, batch.DispatchReceiptId, batch.DispatchGuardRevision, capability, signingId, jwsDigest)
            && DistinctOperations(state, batch.IssueReceiptId, batch.DispatchReceiptId)
            && !EarlierHoldRestricts(state, owner, batch);
    }

    private static bool EarlierHoldRestricts(TenantGovernanceGuardState state, GovernanceDeletionState deletion, GovernanceBatchState batch)
    {
        foreach (GovernanceHoldState hold in state.Holds)
        {
            if (!hold.Active || !Overlaps(hold.Scope, deletion.Scope))
            {
                continue;
            }

            GovernanceProtocolReceipt[] receipts = state.Receipts.Where(receipt => receipt is not null && receipt.Status == "Committed" && receipt.ReferenceId == hold.HoldId).ToArray();
            if (receipts.Length != 1 || !Text(receipts[0].OperationId) || !Text(receipts[0].ReceiptId)
                || receipts[0].IntentDigest is not { Length: 64 } || !receipts[0].IntentDigest.All(char.IsAsciiHexDigit)
                || receipts[0].GuardHighWater <= batch.DispatchGuardRevision || receipts[0].GuardHighWater > state.Revision)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ValidHold(GovernanceHoldState? hold, string tenantId)
        => hold is not null && Text(hold.HoldId) && Text(hold.DispositionVersion)
            && ValidScope(hold.Scope, tenantId)
            && (hold.Active ? hold.ReleaseReceiptId == "" : Text(hold.ReleaseReceiptId));

    private static bool ValidScope(GovernanceScopeV1? scope, string tenantId)
        => scope is not null && scope.TenantId == tenantId
            && (scope.Kind == "ExactInteraction" && Text(scope.AgentInteractionId) && scope.SourceConversationId == ""
                || scope.Kind == "SourceDeletionExactConversation" && Text(scope.SourceConversationId) && scope.AgentInteractionId == "");

    private static bool Text(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048) { return false; }
        try { _ = StrictUtf8.GetByteCount(value); return true; }
        catch (EncoderFallbackException) { return false; }
    }

    private static bool DistinctOperations(TenantGovernanceGuardState state, string issueReceiptId, string dispatchReceiptId)
    {
        GovernanceProtocolReceipt? issue = state.Receipts.SingleOrDefault(receipt => receipt?.ReceiptId == issueReceiptId);
        GovernanceProtocolReceipt? dispatch = state.Receipts.SingleOrDefault(receipt => receipt?.ReceiptId == dispatchReceiptId);
        return issue is not null && dispatch is not null
            && !string.IsNullOrWhiteSpace(issue.OperationId) && !string.IsNullOrWhiteSpace(dispatch.OperationId)
            && issue.OperationId != dispatch.OperationId;
    }

    private static bool Committed(TenantGovernanceGuardState state, string receiptId, long revision, DeletionBatchCapabilityV1 capability, string signingId, string jwsDigest)
    {
        GovernanceProtocolReceipt[] receipts = state.Receipts.Where(receipt => receipt is not null && receipt.ReceiptId == receiptId).ToArray();
        if (receipts.Length != 1)
        {
            return false;
        }

        GovernanceProtocolReceipt receipt = receipts[0];
        return receipt.Status == "Committed" && receipt.GuardHighWater == revision && receipt.ReferenceId == capability.BatchId
            && receipt.Capability == capability && receipt.SigningRequestId == signingId
            && string.Equals(receipt.DetachedJwsDigest, jwsDigest, StringComparison.Ordinal);
    }

    private static bool Overlaps(GovernanceScopeV1 left, GovernanceScopeV1 right)
        => left.TenantId == right.TenantId && (left.Kind != right.Kind || left == right);

    private static bool SameTargets(IReadOnlyList<GovernanceProtectionTarget> left, IReadOnlyList<ProtectionTarget> right)
    {
        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Count; index++)
        {
            GovernanceProtectionTarget item = left[index];
            ProtectionTarget expected = right[index];
            if (item is null || expected is null || item.TenantId != expected.TenantId || item.AgentInteractionId != expected.AgentInteractionId
                || item.TargetProtectionKeyAlias != expected.TargetProtectionKeyAlias)
            {
                return false;
            }
        }

        return true;
    }

    private static string Header(PublishedDeletionCapabilityTrust trust)
    {
        var text = new StringBuilder("{\"alg\":\"ES256\",\"anchorVersion\":");
        Append(text, trust.PublicAnchorVersion);
        text.Append(",\"keyVersion\":");
        Append(text, trust.CapabilityKeyVersion);
        text.Append(",\"kid\":");
        Append(text, trust.PublicAnchorId);
        text.Append(",\"typ\":\"DeletionBatchCapabilityV1\"}");
        return Base64Url(StrictUtf8.GetBytes(text.ToString()));
    }

    private static void Append(StringBuilder text, string value)
    {
        _ = StrictUtf8.GetByteCount(value);
        text.Append('"');
        foreach (char character in value)
        {
            switch (character)
            {
                case '"':
                    text.Append("\\\"");
                    break;
                case '\\':
                    text.Append("\\\\");
                    break;
                case '\b':
                    text.Append("\\b");
                    break;
                case '\t':
                    text.Append("\\t");
                    break;
                case '\n':
                    text.Append("\\n");
                    break;
                case '\f':
                    text.Append("\\f");
                    break;
                case '\r':
                    text.Append("\\r");
                    break;
                default:
                    if (character < 0x20)
                    {
                        text.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        text.Append(character);
                    }

                    break;
            }
        }

        text.Append('"');
    }

    private static bool Verify(string header, ReadOnlySpan<byte> payload, string detachedJws, byte[] subjectPublicKeyInfo)
    {
        byte[]? input = null;
        try
        {
            if (detachedJws is null || detachedJws.Length != (long)header.Length + 88
                || !detachedJws.AsSpan(0, header.Length).SequenceEqual(header)
                || detachedJws[header.Length] != '.' || detachedJws[header.Length + 1] != '.')
            {
                return false;
            }

            string signatureText = detachedJws[(header.Length + 2)..];
            byte[] signature = Decode(signatureText);
            if (signature.Length != 64 || !string.Equals(Base64Url(signature), signatureText, StringComparison.Ordinal))
            {
                return false;
            }

            using ECDsa key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(subjectPublicKeyInfo, out int bytesRead);
            if (bytesRead != subjectPublicKeyInfo.Length || key.KeySize != 256 || key.ExportParameters(false).Curve.Oid.Value != CurveOid)
            {
                return false;
            }

            input = Encoding.ASCII.GetBytes(header + "." + Base64Url(payload));
            return key.VerifyData(input, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException or ArgumentException or PlatformNotSupportedException)
        {
            return false;
        }
        finally
        {
            if (input is not null)
            {
                CryptographicOperations.ZeroMemory(input);
            }
        }
    }

    private static string Base64Url(ReadOnlySpan<byte> bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Decode(string value)
        => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - (value.Length % 4)) % 4));
}
