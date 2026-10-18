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
    private const string CurveOid = "1.2.840.10045.3.1.7";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly PublishedDeletionCapabilityTrust[] _publishedTrust;
    private readonly IGovernanceScopeGuard _guard;

    /// <summary>Creates the authority from injected published trust and a guard that is reread on every check.</summary>
    /// <param name="publishedTrust">Published trust entries. Exactly one unrevoked match may verify a capability.</param>
    /// <param name="guard">Fresh guard reader. Each dispatch check reads it twice and keeps neither result.</param>
    public PublishedTrustDeletionConsumptionAuthority(IReadOnlyList<PublishedDeletionCapabilityTrust> publishedTrust, IGovernanceScopeGuard guard)
    {
        ArgumentNullException.ThrowIfNull(publishedTrust);
        ArgumentNullException.ThrowIfNull(guard);
        if (publishedTrust.Any(static entry => entry is null))
        {
            throw new ArgumentException("Published trust contains an absent entry.", nameof(publishedTrust));
        }

        _publishedTrust = publishedTrust.ToArray();
        _guard = guard;
    }

    /// <inheritdoc />
    public Task<bool> AuthorizeOperationAsync(string tenantId, string identity, string operation, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    /// <inheritdoc />
    public Task<bool> ValidateStateAsync(string tenantId, long revision, string exactStateDigest, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    /// <inheritdoc />
    public Task<bool> RecordRevisionAsync(string tenantId, long expectedRevision, long nextRevision, string exactStateDigest, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

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
            if (firstBytes is null || second is null || !firstBytes.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(second)))
            {
                return false;
            }

            return Admitted(second, captured);
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
    public Task<bool> VerifyAdmissionBlockAsync(DeletionBatchBlockRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    /// <inheritdoc />
    public Task<bool> VerifyRevocationAsync(DeletionCapabilityRevocationEnvelope envelope, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    /// <inheritdoc />
    public Task<bool> VerifyActivationAsync(DeletionReattestationActivation activation, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

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
        if (state.TenantId != capability.TenantId || state.Deletions is null || state.Receipts is null || state.Holds is null || state.CompromisedKeyVersions is null)
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
        if (owner is null || owner.IntegrityCompromised || owner.RequestId != capability.DeletionRequestId || owner.SealId != capability.DestructionSealId
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

        GovernanceBatchState[] active = owner.Batches.Where(item => item is not null && item.ProtectionOutcome == "Unconsumed"
            && !string.IsNullOrWhiteSpace(item.DispatchReceiptId)).ToArray();
        return active.Length == 1 && active[0] == batch
            && Committed(state, batch.IssueReceiptId, batch.IssuedGuardRevision, capability, signingId, jwsDigest)
            && Committed(state, batch.DispatchReceiptId, batch.DispatchGuardRevision, capability, signingId, jwsDigest)
            && DistinctOperations(state, batch.IssueReceiptId, batch.DispatchReceiptId)
            && !EarlierHoldRestricts(state, owner, batch);
    }

    private static bool EarlierHoldRestricts(TenantGovernanceGuardState state, GovernanceDeletionState deletion, GovernanceBatchState batch)
    {
        foreach (GovernanceHoldState hold in state.Holds)
        {
            if (hold is null || !hold.Active || hold.Scope is null || deletion.Scope is null || !Overlaps(hold.Scope, deletion.Scope))
            {
                continue;
            }

            GovernanceProtocolReceipt[] receipts = state.Receipts.Where(receipt => receipt is not null && receipt.Status == "Committed" && receipt.ReferenceId == hold.HoldId).ToArray();
            if (receipts.Length != 1 || receipts[0].GuardHighWater <= batch.DispatchGuardRevision)
            {
                return true;
            }
        }

        return false;
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
