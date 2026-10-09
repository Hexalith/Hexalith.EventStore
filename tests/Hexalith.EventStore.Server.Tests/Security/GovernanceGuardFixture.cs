using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Serialized actual technical owner over modeled CAS/source authorities. It supplies no real decisions, custody, writer installation or backend qualification.</summary>
internal sealed class GovernanceGuardFixture
{
    internal GuardedTransactionFixture Backend { get; } = new();
    internal IGovernanceGuardAuthority Authority { get; } = Substitute.For<IGovernanceGuardAuthority>();
    internal GovernanceScopeGuardOwner Owner { get; }
    internal string Disposition { get; set; } = "Open";
    internal string DispositionVersion { get; set; } = "";
    internal bool DenyEffects { get; set; }
    internal Func<GovernanceGuardEvidence, GovernanceGuardTransition, GovernanceGuardEvidence>? ChangeEvidence { get; set; }
    internal GovernanceProtocolReceipt? OriginalAcceptance { get; set; }
    internal string AppendResource { get; set; } = "resource-b";
    private readonly Dictionary<string, GovernanceProtocolReceipt> _acceptedResources = [];
    internal GovernanceProtectionTarget ViolationTarget { get; set; } = new("tenant-a", "interaction-b", "alias-b");
    internal GovernanceScopeV1 Scope { get; } = new("tenant-a", "SourceDeletionExactConversation", "", "conversation-a");
    internal TenantGovernanceGuardState State => JsonSerializer.Deserialize<TenantGovernanceGuardState>(JsonSerializer.Deserialize<GuardedStateCell>(Backend.Stored[Backend.Key(Backend.Target.GuardCellId)])!.Value)!;
    private int _operation;
    internal GovernanceGuardFixture()
    {
        var state = new TenantGovernanceGuardState("tenant-a", Backend.Target.InstallationId, 1, "epoch-a", "legacy-revoked", "writers-installed", null, [], [], [], [], [], []);
        Backend.Stored[Backend.Key(Backend.Target.GuardCellId)] = JsonSerializer.SerializeToUtf8Bytes(new GuardedStateCell("tenant-a", Backend.Target.InstallationId, Backend.Target.GuardCellId, 1, JsonSerializer.SerializeToUtf8Bytes(state)));
        Authority.ReadLookupAsync(Arg.Any<GovernanceGuardTransition>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => Evidence(call.Arg<GovernanceGuardTransition>(), call.ArgAt<string>(1), call.ArgAt<string>(2), true));
        Authority.ReadAsync(Arg.Any<GovernanceGuardTransition>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TenantGovernanceGuardState>(), Arg.Any<CancellationToken>()).Returns(call =>
            DenyEffects ? null : Evidence(call.Arg<GovernanceGuardTransition>(), call.ArgAt<string>(1), call.ArgAt<string>(2), false, call.Arg<TenantGovernanceGuardState>()));
        Owner = new(Backend.Owner, TimeProvider.System, Authority);
    }
    internal GovernanceGuardTransition Command(GovernanceGuardOperation operation, GovernanceOrdinalCommand? ordinal = null, GovernanceBatchCommand? batch = null,
        GovernanceHoldCommand? hold = null, string authorization = "", string reference = "", GovernanceScopeV1? scope = null, GovernanceWriteFacts? write = null, DirectoryRepairBoundary? repair = null)
        => new("tenant-a", "operation-" + Interlocked.Increment(ref _operation).ToString(System.Globalization.CultureInfo.InvariantCulture), operation, State.Revision,
            State.EpochId, "deletion-a", scope, write, repair, ordinal, hold, batch, authorization, reference);
    internal GovernanceWriteFacts Facts(DirectoryWriteKind kind = DirectoryWriteKind.ContentAppend, string interaction = "interaction-b")
        => new("tenant-a", interaction, "conversation-a", "conversation-a", "tenant-a:directory:directory-a", "permit-a", "effect-a", 1, kind, State.EpochId, "independent-original-source");
    internal GovernanceOrdinalCommand Ordinal(long ordinal = 1, string cut = "cut-a", string token = "token-a", string owner = "", string violation = "", string kind = "Content", string resource = "resource-b", IReadOnlyList<string>? invalidated = null, string noCut = "")
        => new(ordinal, cut, token, owner, GovernanceScopeGuardReducer.ObligationDigest(State.Deletions.SingleOrDefault()?.ObligationIds ?? ["initial-obligation"]), violation, kind,
            Acceptance(resource)?.AcceptedAtAdmissionFenceOrdinal ?? 0, Acceptance(resource)?.GuardHighWater ?? 2, resource, invalidated ?? [], noCut);
    internal GovernanceBatchCommand Batch(string kind = "AcceptedSet", long ordinal = 0, IReadOnlyList<GovernanceProtectionTarget>? targets = null)
    {
        targets ??= [new("tenant-a", "interaction-a", "alias-a")];
        var batch = new GovernanceBatchCommand("pending", kind, ordinal, targets, GovernanceScopeGuardReducer.ManifestDigest(targets), 1, "healthy-key-a", Hash("attestation-a"), "", "", [], 0);
        string seal = State.Deletions.SingleOrDefault()?.SealId ?? "";
        if (seal == "") { seal = HashObject(new[] { "tenant-a", "deletion-a", GovernanceScopeGuardReducer.PredicateDigest(Scope), "DestructionSealed" }); }
        return batch with { BatchId = GovernanceScopeGuardReducer.BatchId("deletion-a", seal, batch) };
    }
    internal async Task<GovernanceProtocolReceipt> Apply(GovernanceGuardTransition command, IReadOnlyList<GuardedStateMutation>? targets = null)
        => (await Owner.ExecuteAsync(command, targets ?? [], TestContext.Current.CancellationToken))!;
    internal async Task PrepareBoundAsync(bool seal = false, DirectoryWriteKind kind = DirectoryWriteKind.ContentAppend)
    {
        OriginalAcceptance = await Apply(Command(GovernanceGuardOperation.AppendWrite, write: Facts(kind)), [new("original-source", 0, GuardedTransactionFixture.Hash([]), "sealed-original"u8.ToArray())]);
        _acceptedResources["resource-b"] = OriginalAcceptance;
        AppendResource = "resource-a";
        _acceptedResources["resource-a"] = await Apply(Command(GovernanceGuardOperation.AppendWrite, write: Facts(kind)), [new("original-source-a", 0, GuardedTransactionFixture.Hash([]), "sealed-original-a"u8.ToArray())]);
        AppendResource = "resource-b";
        (await Apply(Command(GovernanceGuardOperation.AuthorizeAdmissionFence, scope: Scope, authorization: "admission-a"))).Status.ShouldBe("Committed");
        (await Apply(Command(GovernanceGuardOperation.CommitAdmissionFence, scope: Scope, authorization: "admission-a"))).Status.ShouldBe("Committed");
        await EffectiveAsync(); await BindAsync("binding-a", "cut-a", "token-a");
        if (seal) { await SealAsync(); }
    }
    internal async Task EffectiveAsync()
    {
        foreach (string owner in new[] { "directory", "interaction" })
        { (await Apply(Command(GovernanceGuardOperation.RecordOwnerCycleEffective, ordinal: Ordinal(State.Deletions.Single().Ordinal, owner: owner)))).Status.ShouldBe("Committed"); }
    }
    internal async Task BindAsync(string authorization, string cut, string token)
    {
        var ordinal = Ordinal(State.Deletions.Single().Ordinal, cut, token);
        (await Apply(Command(GovernanceGuardOperation.AuthorizeContentBinding, ordinal: ordinal, authorization: authorization))).Status.ShouldBe("Committed");
        (await Apply(Command(GovernanceGuardOperation.CommitContentBinding, ordinal: ordinal, authorization: authorization))).Status.ShouldBe("Committed");
    }
    internal async Task SealAsync()
    {
        var batch = Batch();
        (await Apply(Command(GovernanceGuardOperation.AuthorizeDestructionStart, batch: batch, authorization: "seal-a"))).Status.ShouldBe("Committed");
        (await Apply(Command(GovernanceGuardOperation.CommitDestructionStart, batch: batch, authorization: "seal-a"))).Status.ShouldBe("Committed");
    }
    internal async Task IssueConsumeAsync(GovernanceBatchCommand batch, string suffix)
    {
        batch = Attest(batch);
        (await Apply(Command(GovernanceGuardOperation.RecordBatchIssued, batch: batch))).Status.ShouldBe("Committed");
        (await Apply(Command(GovernanceGuardOperation.AuthorizeDispatch, batch: batch, authorization: "dispatch-" + suffix))).Status.ShouldBe("Committed");
        (await Apply(Command(GovernanceGuardOperation.CommitDispatch, batch: batch, authorization: "dispatch-" + suffix))).Status.ShouldBe("Committed");
        batch = batch with { ProtectionOutcome = "Consumed", ProtectionReceiptId = "protection-" + suffix, TargetReceiptIds = batch.Targets.Select((_, index) => "target-" + suffix + index).ToArray() };
        (await Apply(Command(GovernanceGuardOperation.RecordProtectionOutcome, batch: batch))).Status.ShouldBe("Committed");
    }
    internal GovernanceBatchCommand Attest(GovernanceBatchCommand batch)
    {
        var payload = new DeletionBatchCapabilityV1("qualified-issuer", "protection-audience", "tenant-a", "deletion-a", State.Deletions.Single().SealId,
            batch.BatchKind, batch.BatchOrdinal, batch.BatchId, batch.ManifestDigest, "tenant-a-governance-guard", State.Revision, batch.AttestationOrdinal, 1, batch.CapabilityKeyVersion);
        string jws = "synthetic.detached-public-" + batch.AttestationOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture) + batch.CapabilityKeyVersion;
        return batch with { Capability = payload, DetachedJws = jws, AttestationDigest = Hash(jws), SigningRequestId = DeletionBatchCapabilityIdentity.SigningRequestId(payload) };
    }
    private GovernanceProtocolReceipt? Acceptance(string resource) => resource == "resource-b" ? OriginalAcceptance : _acceptedResources.GetValueOrDefault(resource) ?? OriginalAcceptance;
    private GovernanceGuardEvidence Evidence(GovernanceGuardTransition command, string digest, string targetDigest, bool lookup, TenantGovernanceGuardState? snapshot = null)
    {
        snapshot ??= State;
        var acceptance = Acceptance(command.Ordinal?.ResourceId ?? "resource-b");
        var evidence = new GovernanceGuardEvidence("tenant-a", digest, targetDigest, lookup ? "lookup-authority" : "effect-authority",
            lookup ? "private-original-lookup" : command.Hold?.ReleaseReceiptId is { Length: > 0 } release ? release : command.Batch?.ProtectionReceiptId is { Length: > 0 } receipt ? receipt : "independent-exact-authority",
            Backend.Now, Backend.Now.AddMinutes(1), ["directory", "interaction"], ["initial-obligation"], command.Repair?.Cohort ?? [], "writers-installed", "legacy-revoked",
            "current-zero", snapshot.Deletions.SingleOrDefault()?.Ordinal ?? 1, lookup ? "" : Disposition, lookup ? "" : DispositionVersion,
            snapshot.Deletions.SelectMany(value => value.Batches).Where(value => value.ProtectionReceiptId != "").Select(value => value.ProtectionReceiptId).ToArray(), command.RevocationReceipt?.ReceiptId ?? "exact-protection-block")
            { AppendResourceId = AppendResource, OriginalAcceptance = acceptance, ViolationAcceptanceOperationId = acceptance?.OperationId ?? "",
                ViolationTargetMutationDigest = acceptance?.AcceptedTargetMutationDigest ?? "", ViolationWriteFacts = acceptance?.AcceptedWriteFacts, ViolationResourceId = command.Ordinal?.ResourceId ?? "", ViolationProtectionTarget = ViolationTarget,
                CapabilityIssuer = "qualified-issuer", CapabilityAudience = "protection-audience", GuardStreamId = "tenant-a-governance-guard", NoCutReceiptId = "authenticated-no-cut", RevocationReceipt = command.RevocationReceipt };
        if (lookup) { return evidence with { RequiredOwnerIds = [], ObligationIds = [], RequiredOutcomeReceiptIds = [], OriginalAcceptance = null, ViolationWriteFacts = null, ViolationProtectionTarget = null, ZeroOrdinal = 0, ViolationResourceId = "" }; }
        return ChangeEvidence is null ? evidence : ChangeEvidence(evidence, command);
    }
    internal static string Hash(string text) => GuardedTransactionFixture.Hash(System.Text.Encoding.UTF8.GetBytes(text));
    private static string HashObject<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
