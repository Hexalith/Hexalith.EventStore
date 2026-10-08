using System.Reflection;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.EventStore.Testing.Fakes;
using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Actual actor/state-manager source fixture; synthetic authenticated guard and physical backend, no live qualification.</summary>
internal sealed class DeletionConsumptionFixture
{
    internal InMemoryStateManager Backend { get; } = new();
    internal IDeletionConsumptionAuthority Authority { get; } = Substitute.For<IDeletionConsumptionAuthority>();
    internal IAtomicDeletionManifestProvider Provider { get; } = Substitute.For<IAtomicDeletionManifestProvider>();
    internal Dictionary<string, DeletionManifestProviderResult> Retained { get; } = [];
    internal long Anchor { get; set; }
    internal string AnchorDigest { get; set; } = DeletionConsumptionIdentity.Digest(new DeletionConsumptionLedger("tenant-a", 0, 0, [], [], []));
    internal bool LoseResponse { get; set; }
    internal Func<DeletionManifestProviderResult, DeletionManifestProviderResult>? AlterResult { get; set; }
    internal DeletionConsumptionActor Actor { get; }
    internal DeletionConsumptionFixture()
    {
        Authority.AuthorizeOperationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.ValidateStateAsync("tenant-a", Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<long>() == Anchor && call.ArgAt<string>(2) == AnchorDigest);
        Authority.RecordRevisionAsync("tenant-a", Arg.Any<long>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => {
            if (call.ArgAt<long>(1) != Anchor || call.ArgAt<long>(2) != Anchor + 1) { return false; } Anchor++; AnchorDigest = call.ArgAt<string>(3); return true;
        });
        Authority.VerifyDispatchAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.VerifyAdmissionBlockAsync(Arg.Any<DeletionBatchBlockRequest>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.VerifyRevocationAsync(Arg.Any<DeletionCapabilityRevocationEnvelope>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.VerifyActivationAsync(Arg.Any<DeletionReattestationActivation>(), Arg.Any<CancellationToken>()).Returns(true);
        Provider.ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => {
            var request = call.Arg<DeletionBatchConsumptionRequest>(); string receipt = call.Arg<string>();
            var result = new DeletionManifestProviderResult(request.Capability.TenantId, request.Capability.BatchId, receipt,
                DeletionManifestProviderState.Consumed, request.Targets.Select((target, index) => new DeletionTargetReceipt(target, request.Capability.BatchId, "destroyed-" + index)).ToArray());
            Retained[receipt] = result;
            if (LoseResponse) { return Task.FromException<DeletionManifestProviderResult>(new HttpRequestException("Controlled lost physical outcome response.")); }
            return Task.FromResult(AlterResult?.Invoke(result) ?? result);
        });
        Provider.LookupAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => {
            var request = call.Arg<DeletionBatchConsumptionRequest>(); string receipt = call.Arg<string>();
            return Retained.TryGetValue(receipt, out var result) ? result : new(request.Capability.TenantId, request.Capability.BatchId, receipt, DeletionManifestProviderState.NotStarted, []);
        });
        Actor = Create(Backend, Authority, Provider);
    }
    internal static DeletionConsumptionActor Create(IActorStateManager backend, IDeletionConsumptionAuthority? authority = null, IAtomicDeletionManifestProvider? provider = null)
    {
        var actor = new DeletionConsumptionActor(ActorHost.CreateForTest<DeletionConsumptionActor>(new ActorTestOptions {
            ActorId = new(DeletionConsumptionActor.GetActorId("tenant-a")) }), authority, provider);
        typeof(Dapr.Actors.Runtime.Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)!.SetValue(actor, backend); return actor;
    }
    internal static DeletionBatchConsumptionRequest Request(string batch = "batch-1", string version = "key-v1", long attestation = 1, IReadOnlyList<ProtectionTarget>? targets = null)
    {
        targets ??= new[] { new ProtectionTarget("tenant-a", "interaction-a", "alias-a"), new ProtectionTarget("tenant-a", "interaction-b", "alias-b") };
        return new(new("issuer", "protection-v1", "tenant-a", "request-1", "seal-1", "accepted", 0, batch,
            DeletionConsumptionIdentity.TargetDigest(targets), "tenant-a:governance:guard-1", 7, attestation, 1, version), "candidate-signature-artifact", 8, "dispatch-" + attestation, 9 + attestation, targets);
    }
    internal static DeletionCapabilityRevocationEnvelope Revocation(string version = "key-v1", long revision = 1) => new("issuer", "registrar-v1", "tenant-a",
        "DeletionBatchCapabilitySigningKey", version, revision, 1, "revoke-" + version + "-" + revision, new string('A', 64));
    internal static DeletionReattestationActivation Activation(DeletionConsumptionOutcome blocked, DeletionBatchConsumptionRequest replacement, string operation = "activate-1") =>
        new(operation, blocked.ReceiptId!, blocked.KeyBlockSetRevision, "guard-replacement-" + replacement.Capability.AttestationOrdinal, replacement);
}
