using System.Runtime.InteropServices;
using System.Reflection;
using System.Security.Cryptography;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Server.Events;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Exercises the actual shared evolution/source/claim/local actor protocol and independent persisted end state.</summary>
public sealed class DaprLogicalReplayTests
{
    private const string Operation = "logical-replay:operation:v1";
    private const string Final = "logical-replay:final:v1";

    /// <summary>Refuses disposed registry authority for empty proofs and owner admission without poisoning shared loss.</summary>
    [Fact]
    public async Task DisposedRegistryEmptyTargetRefusesBeforeSigningOrSavingWithoutPoisoningSharedLoss()
    {
        using var fixture = new DaprLogicalReplayFixture(0); fixture.Metadata = null; var store = new DaprReplayTestStore();
        await using var owner = new DaprReplayOperationOwner(store.Manager, "tenant", "operation");
        DaprLogicalSourceBinding binding = await fixture.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        fixture.Registry.Dispose(); fixture.Registry.CapabilityLoss.RequireNoObservedLoss(); var budget = new EventBufferBudget();
        await Should.ThrowAsync<ObjectDisposedException>(() => fixture.Source.ReadFirstPageAsync(binding, 1, fixture.Trust, fixture.Key, budget, CancellationToken.None));
        await Should.ThrowAsync<ObjectDisposedException>(() => owner.BeginAsync(fixture.Source, binding, fixture.Trust, "owner", null, CancellationToken.None));
        fixture.Registry.CapabilityLoss.RequireNoObservedLoss(); fixture.ValidationCalls.ShouldBe(0); store.Saves.ShouldBe(0); store.Durable.ShouldBeEmpty(); budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Refuses initial orphan participants without creating or overwriting an operation pointer.</summary>
    [Theory]
    [InlineData("ledger")]
    [InlineData("null-ledger")]
    [InlineData("response")]
    [InlineData("final")]
    public async Task BeginWithoutOperationRefusesOrphanParticipantsBeforeSavingAnyPointer(string orphan)
    {
        using var fixture = new DaprLogicalReplayFixture(); var store = new DaprReplayTestStore();
        await using var owner = new DaprReplayOperationOwner(store.Manager, "tenant", "operation");
        DaprLogicalSourceBinding binding = await fixture.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        if (orphan == "ledger") { store.Put("logical-replay:ledger:1", new DaprReplayPageLedger(1, 1, new byte[32], new byte[32], new byte[32], 1, 1, 1, new byte[32], true)); }
        else if (orphan == "null-ledger") { store.Put<DaprReplayPageLedger?>("logical-replay:ledger:1", null); }
        else { store.Put(orphan == "final" ? Final : "logical-replay:response:1", "orphan"u8.ToArray()); }
        var expected = store.Durable.ToDictionary(static value => value.Key, static value => value.Value.ToArray());
        using var result = await owner.BeginAsync(fixture.Source, binding, fixture.Trust, "owner", null, CancellationToken.None);
        result.Outcome.ShouldBe(DaprReplayCommitOutcome.Indeterminate); result.IsComplete.ShouldBeFalse();
        fixture.ValidationCalls.ShouldBe(0); store.Saves.ShouldBe(0); store.Durable.ContainsKey(Operation).ShouldBeFalse();
        store.Durable.Keys.ShouldBe(expected.Keys); foreach (string key in expected.Keys) { store.Durable[key].ShouldBe(expected[key]); }
    }

    /// <summary>Refuses a present null operation without treating it as absence or replacing its durable participant.</summary>
    [Fact]
    public async Task NullPersistedOperationRefusesBeginAndExecutionWithoutOverwrite()
    {
        using var fixture = new DaprLogicalReplayFixture(); var store = new DaprReplayTestStore();
        await using var owner = new DaprReplayOperationOwner(store.Manager, "tenant", "operation");
        DaprLogicalSourceBinding binding = await fixture.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        store.Put<DaprReplayOperationRecord?>(Operation, null); byte[] original = store.Durable[Operation].ToArray();
        await Should.ThrowAsync<InvalidOperationException>(() => owner.BeginAsync(fixture.Source, binding, fixture.Trust, "owner", null, CancellationToken.None));
        await Should.ThrowAsync<InvalidOperationException>(() => owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 1, "first", 1, CancellationToken.None));
        fixture.ValidationCalls.ShouldBe(0); store.Saves.ShouldBe(0); store.Durable[Operation].ShouldBe(original);
    }

    /// <summary>Preserves the ordinary legacy reader path without computing unused logical claim evidence.</summary>
    [Fact]
    public async Task LegacyReaderLeavesApplicationEvidenceOptInWhilePreservingPayloadBytes()
    {
        using var fixture = new DaprLogicalReplayFixture();
        var reader = new DaprLogicalEventReader(fixture.SourceState,
            new NoOpEventPayloadProtectionService(), fixture.Service);
        using DaprLogicalEventView view = await reader.ReadAsync(DaprLogicalReplayFixture.Identity, 1, CancellationToken.None, "r");
        view.ApplicationLogicalDigest.ShouldBeNull(); view.Source.Payload.ShouldBe("{}"u8.ToArray());
    }

    /// <summary>Compares production digests and composed proof fields with independent vectors and checks response ownership.</summary>
    [Fact]
    public async Task AddressedSourceMatchesIndependentApplicationConsumedMetadataAndProofFields()
    {
        using var fixture = new DaprLogicalReplayFixture();
        EventEnvelope source = fixture.Events[1];
        EventLogicalDigest.Compute(source, "json", SHA256.HashData(source.Payload)).ShouldBe(Convert.ToHexStringLower(DaprLogicalReplayFixture.Vector("application", "sha256")));
        DaprLogicalSourceBinding binding = await fixture.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        DaprLogicalClaimCodec.ComputeSourceBindingHash(binding).ShouldBe(DaprLogicalReplayFixture.Vector("source", "sha256"));
        var budget = new EventBufferBudget();
        using DaprLogicalReplayPage page = await fixture.Source.ReadFirstPageAsync(binding, 1, fixture.Trust, fixture.Key, budget, CancellationToken.None);
        using var route = fixture.Trust.VerifyRoute(page.Routes[0].Claim.Span, page.Routes[0].KeyId, page.Routes[0].Signature.Span, budget, CancellationToken.None);
        route.Value.ApplicationLogicalDigest.ToArray().ShouldBe(DaprLogicalReplayFixture.Vector("application", "sha256"));
        route.Value.ConsumedMetadataHash.ToArray().ShouldBe(DaprLogicalReplayFixture.Vector("consumed", "sha256"));
        route.Value.SourceBindingHash.ToArray().ShouldBe(DaprLogicalReplayFixture.Vector("source", "sha256"));
        route.Value.MessageId.ShouldBe(source.MessageId); route.Value.StoredEventType.ShouldBe(source.EventTypeName);
        route.Value.TargetCanonicalType.ShouldBe("evt"); route.Value.TargetPayloadVersion.ShouldBe(1);
        route.Value.RegistryFingerprint.ToArray().ShouldBe(Convert.FromHexString(fixture.Registry.Fingerprint));
        route.Value.EffectivePayloadHash.ToArray().ShouldBe(SHA256.HashData("{}"u8));
        using DaprLogicalResponseOwner response = page.EncodeResponse(budget, CancellationToken.None);
        using var verified = DaprLogicalReplayResponseVerifier.Verify(response.Bytes.Span, binding, fixture.Trust,
            DaprLogicalClaimCodec.ComputeGenesis(page.PrefixFields.SourceBindingHash, fixture.Trust.RegistryFingerprint), budget, CancellationToken.None);
        verified.Value.Count.ShouldBe(1); verified.Value.EndSequence.ShouldBe(1);
        source.Payload.ShouldBe("{}"u8.ToArray()); source.ApplicationPayloadDigest.ShouldBeNull();
        verified.Dispose(); route.Dispose(); page.Dispose(); budget.LiveBytes.ShouldBeGreaterThan(response.Bytes.Length);
        MemoryMarshal.TryGetArray(response.Bytes, out ArraySegment<byte> image).ShouldBeTrue(); response.Dispose();
        image.Array!.ShouldAllBe(static b => b == 0); budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Refuses source, registry, domain and shared loss-scope disagreement before callbacks or event reads.</summary>
    [Theory]
    [InlineData("registry")]
    [InlineData("domain")]
    [InlineData("scope")]
    [InlineData("metadata-offset")]
    [InlineData("host")]
    public async Task SourceTrustOrBindingMismatchRefusesBeforeAnyCatalogCallback(string kind)
    {
        using var fixture = new DaprLogicalReplayFixture();
        DaprLogicalSourceBinding binding = await fixture.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        using DaprLogicalClaimTrust trust = fixture.NewTrust(domain: kind == "domain" ? "foreign" : "d",
            registry: kind == "registry" ? new byte[32] : null, loss: kind == "scope" ? new EventEvolutionCapabilityLoss() : null);
        if (kind == "metadata-offset") { fixture.Metadata = fixture.Metadata! with { LastModified = fixture.Metadata.LastModified.ToUniversalTime() }; }
        if (kind == "host") { binding = binding with { ApplicationId = "foreign-app" }; }
        var budget = new EventBufferBudget();
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Source.ReadFirstPageAsync(binding, 1, trust, fixture.Key, budget, CancellationToken.None));
        fixture.ValidationCalls.ShouldBe(0); fixture.EventReads.ShouldBe(0); budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Detects absent/present actor metadata changes even when every carried logical scalar is zero.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ZeroValuedActorMetadataPresenceToggleRefusesBeforeEmptyProof(bool initiallyPresent)
    {
        using var fixture = new DaprLogicalReplayFixture(0);
        AggregateMetadata zero = new(0, DateTimeOffset.UnixEpoch, null);
        fixture.Metadata = initiallyPresent ? zero : null;
        DaprLogicalSourceBinding binding = await fixture.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        fixture.Metadata = initiallyPresent ? null : zero;
        var budget = new EventBufferBudget();
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Source.ReadFirstPageAsync(binding, 1, fixture.Trust, fixture.Key, budget, CancellationToken.None));
        fixture.ValidationCalls.ShouldBe(0); budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Refuses a complete V2 identity without its required same-save application digest.</summary>
    [Fact]
    public async Task MetadataV2WithoutSameSaveDigestRefusesBeforeCatalogCallbacks()
    {
        using var fixture = new DaprLogicalReplayFixture();
        fixture.Events[1] = fixture.Events[1] with { MetadataVersion = 2, EventTypeName = "evt", EventContractType = "evt", PayloadVersion = 1 };
        DaprLogicalSourceBinding binding = await fixture.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        var budget = new EventBufferBudget();
        (await Should.ThrowAsync<InvalidOperationException>(() => fixture.Source.ReadFirstPageAsync(binding, 1, fixture.Trust, fixture.Key, budget, CancellationToken.None)))
            .Message.ShouldContain("LogicalDigestMismatch");
        fixture.ValidationCalls.ShouldBe(0); budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Checks lost acknowledgement recovery, exact retained retry and the complete durable multi-page result.</summary>
    [Fact]
    public async Task MultiPageLostAckRestartAndExactRetryPreserveCompletePersistedResult()
    {
        using var fixture = new DaprLogicalReplayFixture(2); var store = new DaprReplayTestStore();
        await using var owner = new DaprReplayOperationOwner(store.Manager, "tenant", "operation");
        DaprLogicalSourceBinding binding = await fixture.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        using var begin = await owner.BeginAsync(fixture.Source, binding, fixture.Trust, "owner", null, CancellationToken.None);
        begin.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven); store.SaveMode = "commit-throw";
        using var first = await owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 1, "request-1", 1, CancellationToken.None);
        first.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven); first.IsComplete.ShouldBeFalse();
        byte[] firstImage = first.Response!.Bytes.ToArray(); int callbacks = fixture.ValidationCalls; int saves = store.Saves;
        await using var restarted = new DaprReplayOperationOwner(store.Manager, "tenant", "operation");
        using var retry = await restarted.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 1, "request-1", 1, CancellationToken.None);
        retry.Response!.Bytes.ToArray().ShouldBe(firstImage); fixture.ValidationCalls.ShouldBe(callbacks); store.Saves.ShouldBe(saves);
        store.SaveMode = "normal";
        using var final = await restarted.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 2, "request-2", 1, CancellationToken.None);
        final.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven); final.IsComplete.ShouldBeTrue();
        DaprReplayOperationRecord state = store.Get<DaprReplayOperationRecord>(Operation);
        state.CompletedSequence.ShouldBe(2); state.PageOrdinal.ShouldBe(2); state.IsComplete.ShouldBeTrue();
        store.Get<byte[]>(Final).ShouldBe(final.Response!.Bytes.ToArray());
        store.Get<DaprReplayPageLedger>("logical-replay:ledger:2").Accumulator.ShouldBe(state.Accumulator);
        fixture.Events[1].Payload.ShouldBe("{}"u8.ToArray()); fixture.Events[2].Payload.ShouldBe("{}"u8.ToArray());
    }

    /// <summary>Separates proven no-commit from partial indeterminate save using independent persisted participants.</summary>
    [Theory]
    [InlineData("no-commit", 1)]
    [InlineData("partial", 2)]
    public async Task SaveFailureClassifiesActualParticipantReadbackAndKeepsLastGoodState(string mode, int expectedValue)
    {
        var expected = (DaprReplayCommitOutcome)expectedValue;
        using var fixture = new DaprLogicalReplayFixture(); var store = new DaprReplayTestStore();
        await using var owner = new DaprReplayOperationOwner(store.Manager, "tenant", "operation");
        DaprLogicalSourceBinding binding = await fixture.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        using var begin = await owner.BeginAsync(fixture.Source, binding, fixture.Trust, "owner", null, CancellationToken.None);
        byte[] prior = store.Durable[Operation].ToArray(); store.SaveMode = mode;
        using var failed = await owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 1, "request", 1, CancellationToken.None);
        failed.Outcome.ShouldBe(expected); failed.Response.ShouldBeNull(); store.Durable[Operation].ShouldBe(prior);
        if (expected == DaprReplayCommitOutcome.Indeterminate)
        {
            int callbacks = fixture.ValidationCalls;
            using var retried = await owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 1, "request", 1, CancellationToken.None);
            retried.Outcome.ShouldBe(expected); fixture.ValidationCalls.ShouldBe(callbacks);
        }
    }

    /// <summary>Fences stale owners while preserving already committed prefix progress during takeover.</summary>
    [Fact]
    public async Task TakeoverFencesOldGenerationWithoutRepeatingCommittedPages()
    {
        using var fixture = new DaprLogicalReplayFixture(2); var store = new DaprReplayTestStore();
        await using var owner = new DaprReplayOperationOwner(store.Manager, "tenant", "operation");
        DaprLogicalSourceBinding binding = await fixture.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        using var begin = await owner.BeginAsync(fixture.Source, binding, fixture.Trust, "owner-a", null, CancellationToken.None);
        using var first = await owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner-a", 1, 1, "first", 1, CancellationToken.None);
        using var takeover = await owner.BeginAsync(fixture.Source, binding, fixture.Trust, "owner-b", 1, CancellationToken.None);
        takeover.Generation.ShouldBe(2); int callbacks = fixture.ValidationCalls; int saves = store.Saves;
        await Should.ThrowAsync<InvalidOperationException>(() => owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner-a", 1, 2, "second", 1, CancellationToken.None));
        fixture.ValidationCalls.ShouldBe(callbacks); store.Saves.ShouldBe(saves);
        using var final = await owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner-b", 2, 2, "second", 1, CancellationToken.None);
        final.IsComplete.ShouldBeTrue(); store.Get<DaprReplayOperationRecord>(Operation).CompletedSequence.ShouldBe(2);
    }

    /// <summary>Checks original cancellation before save and committed truth with a withheld response after save.</summary>
    [Fact]
    public async Task PreCommitOriginalCancellationLeavesNoMutationAndPostCommitCancellationPreservesTruthWithoutProof()
    {
        using var fixture = new DaprLogicalReplayFixture(); var store = new DaprReplayTestStore();
        await using var owner = new DaprReplayOperationOwner(store.Manager, "tenant", "operation");
        DaprLogicalSourceBinding binding = await fixture.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        using var begin = await owner.BeginAsync(fixture.Source, binding, fixture.Trust, "owner", null, CancellationToken.None);
        using var cancellation = new CancellationTokenSource(); fixture.OnValidation = token => { token.ShouldBe(cancellation.Token); cancellation.Cancel(); };
        (await Should.ThrowAsync<OperationCanceledException>(() => owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 1, "first", 1, cancellation.Token)))
            .CancellationToken.ShouldBe(cancellation.Token);
        store.Saves.ShouldBe(1); store.Durable.Count.ShouldBe(1);
        fixture.OnValidation = null; using var committedCancellation = new CancellationTokenSource(); store.AfterSave = committedCancellation.Cancel;
        using var committed = await owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 1, "first", 1, committedCancellation.Token);
        committed.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven); committed.OriginatingCancellationObserved.ShouldBeTrue(); committed.Response.ShouldBeNull();
        store.Get<DaprReplayOperationRecord>(Operation).IsComplete.ShouldBeTrue(); store.Durable.ContainsKey(Final).ShouldBeTrue();
    }

    /// <summary>Checks source, capability and key-expiry loss after save without returning stale proof or rolling back.</summary>
    [Theory]
    [InlineData("capability")]
    [InlineData("source")]
    [InlineData("expiry")]
    public async Task PostSaveLossPreservesCommittedTruthAndWithholdsStaleResponse(string loss)
    {
        using var fixture = new DaprLogicalReplayFixture(); var store = new DaprReplayTestStore();
        await using var owner = new DaprReplayOperationOwner(store.Manager, "tenant", "operation");
        DaprLogicalSourceBinding binding = await fixture.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        var time = new DaprLogicalReplayTimeProvider(); using var trust = fixture.NewTrust(time: time);
        using var begin = await owner.BeginAsync(fixture.Source, binding, trust, "owner", null, CancellationToken.None);
        store.AfterSave = () =>
        {
            if (loss == "capability") { fixture.Registry.CapabilityLoss.ObserveViolation(); }
            else if (loss == "expiry") { time.Now = DateTimeOffset.MaxValue; } else { fixture.Metadata = fixture.Metadata! with { ETag = "changed" }; }
        };
        using var result = await owner.ExecutePageAsync(fixture.Source, binding, trust, fixture.Key, "owner", 1, 1, "first", 1, CancellationToken.None);
        result.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven); result.ResponseUnavailable.ShouldBeTrue(); result.Response.ShouldBeNull();
        store.Get<DaprReplayOperationRecord>(Operation).IsComplete.ShouldBeTrue(); store.Durable.ContainsKey(Final).ShouldBeTrue();
    }

    /// <summary>Refuses partial durable participants before callbacks, saving or overwriting any participant.</summary>
    [Theory]
    [InlineData("blob")]
    [InlineData("final")]
    [InlineData("all")]
    public async Task OrphanAtomicParticipantsRefuseBeforeCallbacksSaveOrOverwrite(string corruption)
    {
        using var fixture = new DaprLogicalReplayFixture(); var store = new DaprReplayTestStore();
        await using var owner = new DaprReplayOperationOwner(store.Manager, "tenant", "operation");
        DaprLogicalSourceBinding binding = await fixture.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        using var begin = await owner.BeginAsync(fixture.Source, binding, fixture.Trust, "owner", null, CancellationToken.None);
        byte[] prior = store.Durable[Operation].ToArray();
        using var first = await owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 1, "first", 1, CancellationToken.None);
        var committed = store.Durable.ToDictionary(static x => x.Key, static x => x.Value.ToArray());
        store.Durable.Clear(); store.Durable[Operation] = prior;
        foreach ((string key, byte[] bytes) in committed) { if (key != Operation && (corruption == "all" || (corruption == "blob" && key.Contains("response", StringComparison.Ordinal)) || (corruption == "final" && key == Final))) { store.Durable[key] = bytes; } }
        var expected = store.Durable.ToDictionary(static x => x.Key, static x => x.Value.ToArray()); int calls = fixture.ValidationCalls; int saves = store.Saves;
        using var result = await owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 1, "first", 1, CancellationToken.None);
        result.Outcome.ShouldBe(DaprReplayCommitOutcome.Indeterminate); result.Response.ShouldBeNull(); result.IsComplete.ShouldBeFalse();
        fixture.ValidationCalls.ShouldBe(calls); store.Saves.ShouldBe(saves); store.Durable.Keys.ShouldBe(expected.Keys);
        foreach (string key in expected.Keys) { store.Durable[key].ShouldBe(expected[key]); }
    }

    /// <summary>Checks the empty final result and prevents pointer-only completion after a durable participant disappears.</summary>
    [Fact]
    public async Task EmptyOperationCommitsFinalCountZeroPageAndBeginCannotInventCompletionAfterFinalDeletion()
    {
        using var fixture = new DaprLogicalReplayFixture(0); fixture.Metadata = null; var store = new DaprReplayTestStore();
        await using var owner = new DaprReplayOperationOwner(store.Manager, "tenant", "operation");
        DaprLogicalSourceBinding binding = await fixture.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        using var begin = await owner.BeginAsync(fixture.Source, binding, fixture.Trust, "owner", null, CancellationToken.None);
        using var page = await owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 1, "empty", 1, CancellationToken.None);
        page.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven); page.IsComplete.ShouldBeTrue();
        DaprReplayPageLedger ledger = store.Get<DaprReplayPageLedger>("logical-replay:ledger:1"); ledger.Count.ShouldBe(0); ledger.EndSequence.ShouldBe(0);
        store.Durable.Remove(Final).ShouldBeTrue();
        using var resumed = await owner.BeginAsync(fixture.Source, binding, fixture.Trust, "owner", null, CancellationToken.None);
        resumed.Outcome.ShouldBe(DaprReplayCommitOutcome.Indeterminate); resumed.IsComplete.ShouldBeFalse();
        using var retry = await owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 1, "empty", 1, CancellationToken.None);
        retry.Outcome.ShouldBe(DaprReplayCommitOutcome.Indeterminate); fixture.ValidationCalls.ShouldBe(0);
    }

    /// <summary>Rejects a pinned retry under a changed current signing-key identity before new effects.</summary>
    [Fact]
    public async Task CurrentKeyRotationRefusesRetainedProofWithoutCallbackOrSave()
    {
        using var fixture = new DaprLogicalReplayFixture(); var store = new DaprReplayTestStore();
        await using var owner = new DaprReplayOperationOwner(store.Manager, "tenant", "operation");
        DaprLogicalSourceBinding binding = await fixture.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        using var begin = await owner.BeginAsync(fixture.Source, binding, fixture.Trust, "owner", null, CancellationToken.None);
        using var page = await owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 1, "first", 1, CancellationToken.None);
        using var rotated = fixture.NewTrust(keyId: "rotated-key"); int calls = fixture.ValidationCalls; int saves = store.Saves;
        await Should.ThrowAsync<InvalidOperationException>(() => owner.ExecutePageAsync(fixture.Source, binding, rotated, fixture.Key, "owner", 1, 1, "first", 1, CancellationToken.None));
        fixture.ValidationCalls.ShouldBe(calls); store.Saves.ShouldBe(saves);
    }

    /// <summary>Checks deterministic smaller-page admission and preservation of actor-owned durable images on disposal.</summary>
    [Fact]
    public async Task ComposedAdmissionShrinksContiguousPagesAndDisposalLeavesDurableImagesUnchanged()
    {
        using var fixture = new DaprLogicalReplayFixture(4, 4 * 1024 * 1024); var store = new DaprReplayTestStore();
        var owner = new DaprReplayOperationOwner(store.Manager, "tenant", "operation", 48 * 1024 * 1024);
        DaprLogicalSourceBinding binding = await fixture.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        using var begin = await owner.BeginAsync(fixture.Source, binding, fixture.Trust, "owner", null, CancellationToken.None);
        using var page = await owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 1, "first", 4, CancellationToken.None);
        page.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven); page.IsComplete.ShouldBeFalse();
        DaprReplayPageLedger ledger = store.Get<DaprReplayPageLedger>("logical-replay:ledger:1"); ledger.StartSequence.ShouldBe(1); ledger.Count.ShouldBeLessThan(4);
        ledger.EndSequence.ShouldBe(ledger.Count); store.Get<DaprReplayOperationRecord>(Operation).CompletedSequence.ShouldBe(ledger.EndSequence);
        byte[] durableResponse = store.Durable["logical-replay:response:1"].ToArray(); await owner.DisposeAsync();
        store.Durable["logical-replay:response:1"].ShouldBe(durableResponse);
    }

    /// <summary>Refuses malformed ranges and null durable participants without new callbacks or saves.</summary>
    [Theory]
    [InlineData("pointer")]
    [InlineData("ledger")]
    [InlineData("null-pointer-field")]
    [InlineData("null-ledger-field")]
    [InlineData("null-ledger")]
    public async Task MalformedCommittedRangeRefusesBeforeNewSourceEffects(string corruption)
    {
        using var fixture = new DaprLogicalReplayFixture(2); var store = new DaprReplayTestStore();
        await using var owner = new DaprReplayOperationOwner(store.Manager, "tenant", "operation");
        DaprLogicalSourceBinding binding = await fixture.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        using var begin = await owner.BeginAsync(fixture.Source, binding, fixture.Trust, "owner", null, CancellationToken.None);
        using var first = await owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 1, "first", 1, CancellationToken.None);
        if (corruption == "pointer") { store.Put(Operation, store.Get<DaprReplayOperationRecord>(Operation) with { CompletedSequence = 0 }); }
        else if (corruption == "null-pointer-field") { store.Put(Operation, store.Get<DaprReplayOperationRecord>(Operation) with { Accumulator = null! }); }
        else if (corruption == "null-ledger-field") { store.Put("logical-replay:ledger:1", store.Get<DaprReplayPageLedger>("logical-replay:ledger:1") with { RequestHash = null! }); }
        else if (corruption == "null-ledger") { store.Put<DaprReplayPageLedger?>("logical-replay:ledger:1", null); }
        else { store.Put("logical-replay:ledger:1", store.Get<DaprReplayPageLedger>("logical-replay:ledger:1") with { Count = 2 }); }
        int calls = fixture.ValidationCalls; int saves = store.Saves; byte[] lastGood = store.Durable[Operation].ToArray();
        if (corruption is "pointer" or "null-pointer-field") { await Should.ThrowAsync<InvalidOperationException>(() => owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 2, "second", 1, CancellationToken.None)); }
        else { using var result = await owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 2, "second", 1, CancellationToken.None); result.Outcome.ShouldBe(DaprReplayCommitOutcome.Indeterminate); }
        fixture.ValidationCalls.ShouldBe(calls); store.Saves.ShouldBe(saves); store.Durable[Operation].ShouldBe(lastGood);
    }

    /// <summary>Checks pending capacity across failed deactivation cleanup and safe private-owner release after cache detachment.</summary>
    [Fact]
    public async Task PendingOwnerDeactivationRetainsChargesUntilCacheReleaseAndNeverClearsActorOwnedCopies()
    {
        using var fixture = new DaprLogicalReplayFixture(); var store = new DaprReplayTestStore();
        var owner = new DaprReplayOperationOwner(store.Manager, "tenant", "operation");
        DaprLogicalSourceBinding binding = await fixture.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        using var begin = await owner.BeginAsync(fixture.Source, binding, fixture.Trust, "owner", null, CancellationToken.None);
        store.AfterSave = () => store.FailReads = true;
        using var result = await owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 1, "first", 1, CancellationToken.None);
        result.Outcome.ShouldBe(DaprReplayCommitOutcome.Indeterminate);
        var pending = (DaprReplayPendingTransition)typeof(DaprReplayOperationOwner).GetField("_pending", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
        pending.Prepared.Budget.LiveBytes.ShouldBeGreaterThan(0);
        MemoryMarshal.TryGetArray(pending.Prepared.Response.Bytes, out ArraySegment<byte> privateImage).ShouldBeTrue();
        byte[] cached = store.CachedBytes("logical-replay:response:1"); byte[] cacheImage = cached.ToArray(); byte[] durable = store.Durable["logical-replay:response:1"].ToArray();
        await Should.ThrowAsync<IOException>(() => owner.DisposeAsync().AsTask());
        pending.Prepared.Budget.LiveBytes.ShouldBeGreaterThan(0); privateImage.Array!.ShouldContain(static value => value != 0); cached.ShouldBe(cacheImage);
        store.FailReads = false; await owner.DisposeAsync();
        pending.Prepared.Budget.LiveBytes.ShouldBe(0); privateImage.Array!.ShouldAllBe(static value => value == 0);
        cached.ShouldBe(cacheImage); store.Durable["logical-replay:response:1"].ShouldBe(durable);
    }

    /// <summary>Checks indeterminate readback recovery of the same pinned response without repeated source effects.</summary>
    [Fact]
    public async Task UnavailableReadbackRecoversTheSamePinnedRequestWithoutCallbacksOrAnotherSave()
    {
        using var fixture = new DaprLogicalReplayFixture(); var store = new DaprReplayTestStore();
        await using var owner = new DaprReplayOperationOwner(store.Manager, "tenant", "operation");
        DaprLogicalSourceBinding binding = await fixture.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        using var begin = await owner.BeginAsync(fixture.Source, binding, fixture.Trust, "owner", null, CancellationToken.None);
        store.AfterSave = () => store.FailReads = true;
        using var first = await owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 1, "first", 1, CancellationToken.None);
        first.Outcome.ShouldBe(DaprReplayCommitOutcome.Indeterminate); int callbacks = fixture.ValidationCalls; int saves = store.Saves;
        var pending = (DaprReplayPendingTransition)typeof(DaprReplayOperationOwner).GetField("_pending", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
        byte[] pinned = pending.Prepared.Response.Bytes.ToArray();
        using var unavailable = await owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 1, "first", 1, CancellationToken.None);
        unavailable.Outcome.ShouldBe(DaprReplayCommitOutcome.Indeterminate); pending.Prepared.Budget.LiveBytes.ShouldBeGreaterThan(0);
        store.FailReads = false; store.AfterSave = null;
        using var recovered = await owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 1, "first", 1, CancellationToken.None);
        recovered.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven); recovered.IsComplete.ShouldBeTrue(); recovered.Response!.Bytes.ToArray().ShouldBe(pinned);
        fixture.ValidationCalls.ShouldBe(callbacks);
        store.Saves.ShouldBe(saves);
        pending.Prepared.Budget.LiveBytes.ShouldBeGreaterThan(0);
        recovered.Dispose();
        pending.Prepared.Budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Checks retained quarantine capacity across failed cache detachment and safe release during a healthy retry.</summary>
    [Fact]
    public async Task FailedReadbackCacheDetachmentQuarantinesPrivateCapacityUntilAHealthyRetry()
    {
        using var fixture = new DaprLogicalReplayFixture(); var store = new DaprReplayTestStore();
        await using var owner = new DaprReplayOperationOwner(store.Manager, "tenant", "operation");
        DaprLogicalSourceBinding binding = await fixture.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        using var begin = await owner.BeginAsync(fixture.Source, binding, fixture.Trust, "owner", null, CancellationToken.None);
        using var page = await owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 1, "first", 1, CancellationToken.None);
        int callbacks = fixture.ValidationCalls; int saves = store.Saves;
        store.OnRead = key => { if (key == "logical-replay:response:1") { store.FailReads = true; } };
        await Should.ThrowAsync<IOException>(() => owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 1, "first", 1, CancellationToken.None));
        var quarantine = (List<DaprLogicalResponseOwner>)typeof(DaprReplayOperationOwner).GetField("_cacheQuarantine", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
        quarantine.Count.ShouldBe(1); MemoryMarshal.TryGetArray(quarantine[0].Bytes, out ArraySegment<byte> privateImage).ShouldBeTrue();
        byte[] cached = store.CachedBytes("logical-replay:response:1"); byte[] cacheImage = cached.ToArray();
        using var blocked = await owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 1, "first", 1, CancellationToken.None);
        blocked.Outcome.ShouldBe(DaprReplayCommitOutcome.Indeterminate); privateImage.Array!.ShouldContain(static value => value != 0); cached.ShouldBe(cacheImage);
        store.FailReads = false; store.OnRead = null;
        using var recovered = await owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 1, "first", 1, CancellationToken.None);
        recovered.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven); quarantine.ShouldBeEmpty(); privateImage.Array!.ShouldAllBe(static value => value == 0);
        fixture.ValidationCalls.ShouldBe(callbacks); store.Saves.ShouldBe(saves); cached.ShouldBe(cacheImage);
    }

    /// <summary>Rejects a changed request identity while preserving the committed response and callback/save counts.</summary>
    [Fact]
    public async Task ChangedExactRetryIdentityRefusesBeforeCallbacksOrSave()
    {
        using var fixture = new DaprLogicalReplayFixture(); var store = new DaprReplayTestStore();
        await using var owner = new DaprReplayOperationOwner(store.Manager, "tenant", "operation");
        DaprLogicalSourceBinding binding = await fixture.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        using var begin = await owner.BeginAsync(fixture.Source, binding, fixture.Trust, "owner", null, CancellationToken.None);
        using var page = await owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 1, "first", 1, CancellationToken.None);
        int callbacks = fixture.ValidationCalls; int saves = store.Saves;
        (await Should.ThrowAsync<InvalidOperationException>(() => owner.ExecutePageAsync(fixture.Source, binding, fixture.Trust, fixture.Key, "owner", 1, 1, "changed", 1, CancellationToken.None)))
            .Message.ShouldContain("ReplayRequestConflict");
        fixture.ValidationCalls.ShouldBe(callbacks); store.Saves.ShouldBe(saves);
    }
}
