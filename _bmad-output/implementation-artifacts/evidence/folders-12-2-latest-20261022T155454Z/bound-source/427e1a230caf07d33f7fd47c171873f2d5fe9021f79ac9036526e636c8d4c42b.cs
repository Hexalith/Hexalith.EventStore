using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;

using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Synthetic published trust and guard state. The generated P-256 key is fixture material, not an installed anchor.</summary>
internal sealed class PublishedTrustDispatchFixture
{
    private PublishedTrustDispatchFixture(PublishedDeletionCapabilityTrust trust, DeletionBatchConsumptionRequest request, GovernanceBatchState batch, GovernanceDeletionState deletion, TenantGovernanceGuardState state, IGovernanceScopeGuard guard)
    {
        Trust = trust;
        Request = request;
        Batch = batch;
        Deletion = deletion;
        State = state;
        Guard = guard;
        Authority = new PublishedTrustDeletionConsumptionAuthority([trust], guard);
        Scope = deletion.Scope;
    }

    internal PublishedDeletionCapabilityTrust Trust { get; }

    internal DeletionBatchConsumptionRequest Request { get; }

    internal GovernanceBatchState Batch { get; }

    internal GovernanceDeletionState Deletion { get; }

    internal TenantGovernanceGuardState State { get; }

    internal GovernanceScopeV1 Scope { get; }

    internal IGovernanceScopeGuard Guard { get; }

    internal PublishedTrustDeletionConsumptionAuthority Authority { get; }

    internal static PublishedTrustDispatchFixture Create(string batchId = "batch-a", long issuedRevision = 8, long dispatchRevision = 10, long attestationOrdinal = 1)
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return Create(key, batchId, issuedRevision, dispatchRevision, attestationOrdinal: attestationOrdinal);
    }

    internal static (PublishedTrustDispatchFixture Admitted, DeletionBatchConsumptionRequest Restricted, GovernanceBatchState RestrictedBatch, IReadOnlyList<GovernanceProtocolReceipt> RestrictedReceipts) CreateHoldPair()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        PublishedTrustDispatchFixture admitted = Create(key, "batch-a", 8, 10);
        PublishedTrustDispatchFixture restricted = Create(key, "batch-b", 11, 14, "Containment", 1, "alias-b");
        return (admitted, restricted.Request, restricted.Batch, restricted.State.Receipts);
    }

    internal TenantGovernanceGuardState WithBatch(GovernanceBatchState batch)
        => State with { Deletions = [Deletion with { Batches = [batch] }] };

    internal static GovernanceProtocolReceipt HoldReceipt()
        => new("hold-registration", new string('D', 64), "Committed", 12, 1, "hold-later", "hold-receipt");

    private static PublishedTrustDispatchFixture Create(ECDsa key, string batchId, long issuedRevision, long dispatchRevision,
        string batchKind = "AcceptedSet", long batchOrdinal = 0, string alias = "alias-a", long attestationOrdinal = 1)
    {
        var targets = new ProtectionTarget[] { new("tenant-a", "interaction-a", alias) };
        string manifest = DeletionBatchCapabilityIdentity.TargetManifestDigest(targets);
        var capability = new DeletionBatchCapabilityV1("issuer-a", "protection-a", "tenant-a", "request-1", "seal-1", batchKind, batchOrdinal, batchId, manifest, "tenant-a:guard", issuedRevision - 1, attestationOrdinal, 1, "key-v1");
        var trust = new PublishedDeletionCapabilityTrust("tenant-a", "DeletionBatchCapabilitySigningKey", "key-v1", "issuer-a", "protection-a", "anchor-1", "anchor-v1", key.ExportSubjectPublicKeyInfo(), 3, false);
        string jws = Sign(capability, key, trust);
        var request = new DeletionBatchConsumptionRequest(capability, jws, issuedRevision, "dispatch-" + batchId, dispatchRevision, targets);
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(jws)));
        string signingId = DeletionBatchCapabilityIdentity.SigningRequestId(capability);
        var guardTargets = new GovernanceProtectionTarget[] { new(targets[0].TenantId, targets[0].AgentInteractionId, targets[0].TargetProtectionKeyAlias) };
        var batch = new GovernanceBatchState(batchId, batchKind, batchOrdinal, guardTargets, manifest, attestationOrdinal, "key-v1", digest, "issue-" + batchId, "dispatch-" + batchId, "Unconsumed", "", [], 0, [])
        {
            Capability = capability,
            DetachedJws = jws,
            SigningRequestId = signingId,
            IssuedGuardRevision = issuedRevision,
            DispatchGuardRevision = dispatchRevision,
        };
        var deletion = new GovernanceDeletionState("request-1", new("tenant-a", "ExactInteraction", "interaction-a", ""), "predicate", 1, ["owner-a"], ["obligation-a"], [], [], [], "seal-1", [batch], false, false);
        GovernanceProtocolReceipt issue = Receipt(capability, signingId, digest, "issue-" + batchId, "issue-operation-" + batchId, issuedRevision, batchId);
        GovernanceProtocolReceipt dispatch = Receipt(capability, signingId, digest, "dispatch-" + batchId, "dispatch-operation-" + batchId, dispatchRevision, batchId);
        var state = new TenantGovernanceGuardState("tenant-a", "installation-a", dispatchRevision, "epoch-a", "legacy", "writer", null, [], [deletion], [], [], [issue, dispatch], []);
        var guard = Substitute.For<IGovernanceScopeGuard>();
        guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(state);
        return new PublishedTrustDispatchFixture(trust, request, batch, deletion, state, guard);
    }

    private static GovernanceProtocolReceipt Receipt(DeletionBatchCapabilityV1 capability, string signingId, string digest, string receiptId, string operationId, long revision, string referenceId)
        => new(operationId, new string('D', 64), "Committed", revision, 1, referenceId, receiptId)
        {
            Capability = capability,
            SigningRequestId = signingId,
            DetachedJwsDigest = digest,
        };

    private static string Sign(DeletionBatchCapabilityV1 capability, ECDsa key, PublishedDeletionCapabilityTrust trust)
    {
        string header = Header(trust);
        byte[] payload = DeletionBatchCapabilityIdentity.CanonicalPayload(capability);
        byte[] input = Encoding.ASCII.GetBytes(header + "." + Base64Url(payload));
        byte[] signature = key.SignData(input, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return header + ".." + Base64Url(signature);
    }

    private static string Header(PublishedDeletionCapabilityTrust trust)
    {
        string json = "{\"alg\":\"ES256\",\"anchorVersion\":\"" + trust.PublicAnchorVersion
            + "\",\"keyVersion\":\"" + trust.CapabilityKeyVersion
            + "\",\"kid\":\"" + trust.PublicAnchorId
            + "\",\"typ\":\"DeletionBatchCapabilityV1\"}";
        return Base64Url(Encoding.UTF8.GetBytes(json));
    }

    private static string Base64Url(ReadOnlySpan<byte> bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
