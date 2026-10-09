using Dapr.Actors.Runtime;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.EventStore.Testing.Fakes;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Actual durable tenant actor with synthetic guard/atomic backend; explicit serialized operation orders, no live race or physical qualification claim.</summary>
public sealed class DeletionConsumptionActorTests
{
    /// <summary>A different authorized mutation resolves the independently admitted staged original after restart, while read-only lookup cannot advance it or repeat a physical effect.</summary>
    [Fact]
    public async Task LaterMutationRecoversPreJournalOriginalWithoutItsCaller()
    {
        var f = new DeletionConsumptionFixture { JournalAvailable = false }; var original = DeletionConsumptionFixture.Request();
        await Should.ThrowAsync<InvalidOperationException>(() => f.Actor.RegisterAsync(original));
        var retained = System.Text.Json.JsonSerializer.Serialize(f.Backend.CommittedState.Single().Value.ShouldBeOfType<AnchoredStateTransition>());
        f.Authority.ClearReceivedCalls(); var restarted = DeletionConsumptionFixture.Create(f.Backend, f.Authority, f.Provider);
        (await restarted.LookupAsync("tenant-a", "batch-1")).Status.ShouldBe(DeletionConsumptionStatus.Unavailable);
        await f.Authority.DidNotReceive().RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>());
        f.Anchor.ShouldBe(0); System.Text.Json.JsonSerializer.Serialize(f.Backend.CommittedState.Single().Value).ShouldBe(retained);
        f.JournalAvailable = true;
        (await restarted.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation())).ShouldNotBeNull();
        var saved = f.Backend.CommittedState.Single().Value.ShouldBeOfType<DeletionConsumptionLedger>();
        saved.Revision.ShouldBe(2); DeletionConsumptionIdentity.Digest(saved.Batches.Single().Original).ShouldBe(DeletionConsumptionIdentity.Digest(original)); saved.Revocations.Count.ShouldBe(1);
        (await restarted.LookupAsync("tenant-a", "batch-1")).Status.ShouldBe(DeletionConsumptionStatus.ConsumptionBlocked);
        await f.Provider.DidNotReceive().ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Authenticated block commits before reserve; no physical call occurs and exact retries retain the original receipt.</summary>
    [Fact]
    public async Task AdmissionBlockBeforeReservePersistsAndPreventsEffect()
    {
        var f = new DeletionConsumptionFixture(); var request = DeletionConsumptionFixture.Request(); await f.Actor.RegisterAsync(request);
        var block = new DeletionBatchBlockRequest("tenant-a", "batch-1", "admission-1", "accepted-evidence-1");
        var outcome = await f.Actor.BlockAsync(block); outcome.Status.ShouldBe(DeletionConsumptionStatus.ConsumptionBlocked);
        DeletionConsumptionIdentity.Digest(await f.Actor.ReserveAndConsumeAsync(request)).ShouldBe(DeletionConsumptionIdentity.Digest(outcome)); DeletionConsumptionIdentity.Digest(await f.Actor.BlockAsync(block)).ShouldBe(DeletionConsumptionIdentity.Digest(outcome));
        await f.Provider.DidNotReceive().ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        JsonSerializer.Serialize(f.Backend.CommittedState.Single().Value.ShouldBeOfType<DeletionConsumptionLedger>().Batches.Single().Outcome).ShouldBe(JsonSerializer.Serialize(outcome));
        (await f.Actor.BlockAsync(block with { AdmissionEvidenceId = "changed" })).Status.ShouldBe(DeletionConsumptionStatus.Conflict);
    }
    /// <summary>Reservation wins; a lost response and subsequent admission cannot cancel it or authorize another physical batch.</summary>
    [Fact]
    public async Task ReserveBeforeBlockRecoversSameOriginalVector()
    {
        var f = new DeletionConsumptionFixture { LoseResponse = true }; var request = DeletionConsumptionFixture.Request(); await f.Actor.RegisterAsync(request);
        var reserved = await f.Actor.ReserveAndConsumeAsync(request); reserved.Status.ShouldBe(DeletionConsumptionStatus.ConsumptionReserved);
        DeletionConsumptionIdentity.Digest(await f.Actor.BlockAsync(new("tenant-a", "batch-1", "admission-1", "accepted-1"))).ShouldBe(DeletionConsumptionIdentity.Digest(reserved));
        var consumed = await f.Actor.LookupAsync("tenant-a", "batch-1"); consumed.Status.ShouldBe(DeletionConsumptionStatus.Consumed);
        consumed.ReceiptId.ShouldBe(reserved.ReceiptId); consumed.TargetReceipts.Select(r => r.Target).ShouldBe(request.Targets);
        await f.Provider.Received(1).ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), reserved.ReceiptId!, Arg.Any<CancellationToken>());
        JsonSerializer.Serialize(f.Backend.CommittedState.Single().Value.ShouldBeOfType<DeletionConsumptionLedger>().Batches.Single().Outcome).ShouldBe(JsonSerializer.Serialize(consumed));
    }
    /// <summary>Compromise before registration/reserve blocks; after reservation/consumption preserves the original irreversible vector.</summary>
    [Theory]
    [InlineData("before-register")]
    [InlineData("before-reserve")]
    [InlineData("after-reserve")]
    [InlineData("after-consume")]
    public async Task KeyCompromiseOrdersAgainstOnlyUnconsumedBatch(string schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        var f = new DeletionConsumptionFixture { LoseResponse = schedule == "after-reserve" }; var request = DeletionConsumptionFixture.Request();
        if (schedule == "before-register") { await f.Actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation()); }
        await f.Actor.RegisterAsync(request);
        if (schedule == "before-reserve") { await f.Actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation()); }
        var outcome = await f.Actor.ReserveAndConsumeAsync(request);
        if (schedule.StartsWith("after", StringComparison.Ordinal)) { await f.Actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation()); }
        var durable = await f.Actor.LookupAsync("tenant-a", "batch-1");
        durable.Status.ShouldBe(schedule.StartsWith("before", StringComparison.Ordinal) ? DeletionConsumptionStatus.ConsumptionBlocked : DeletionConsumptionStatus.Consumed);
        if (schedule.StartsWith("before", StringComparison.Ordinal)) { await f.Provider.DidNotReceive().ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()); }
        else { durable.ReceiptId.ShouldBe(outcome.ReceiptId); }
        var state = f.Backend.CommittedState.Single().Value.ShouldBeOfType<DeletionConsumptionLedger>(); state.KeyBlockSetRevision.ShouldBe(1); state.Revocations.Count.ShouldBe(1);
    }
    /// <summary>Partial, changed, wrong order/identity provider evidence remains reserved, never certified consumed.</summary>
    [Theory]
    [InlineData("partial")]
    [InlineData("target")]
    [InlineData("order")]
    [InlineData("batch")]
    [InlineData("reservation")]
    [InlineData("duplicate-id")]
    public async Task MalformedPhysicalVectorCannotBecomeConsumed(string vector)
    {
        var f = new DeletionConsumptionFixture(); f.AlterResult = result => vector switch {
            "partial" => result with { TargetReceipts = result.TargetReceipts.Take(1).ToArray() },
            "target" => result with { TargetReceipts = result.TargetReceipts.Select(r => r with { Target = r.Target with { TargetProtectionKeyAlias = "substitution" } }).ToArray() },
            "order" => result with { TargetReceipts = result.TargetReceipts.Reverse().ToArray() },
            "batch" => result with { BatchId = "wrong" }, "duplicate-id" => result with { TargetReceipts = result.TargetReceipts.Select(r => r with { ReceiptId = "same-receipt" }).ToArray() }, _ => result with { ReservationReceiptId = "wrong" }
        };
        var request = DeletionConsumptionFixture.Request(); await f.Actor.RegisterAsync(request);
        (await f.Actor.ReserveAndConsumeAsync(request)).Status.ShouldBe(DeletionConsumptionStatus.ConsumptionReserved);
        var state = f.Backend.CommittedState.Single().Value.ShouldBeOfType<DeletionConsumptionLedger>(); state.Batches.Single().Outcome.TargetReceipts.ShouldBeEmpty();
    }
    /// <summary>Replacement compromise wins the atomic compare and leaves the batch in the exact new-key block without clearing either global block.</summary>
    [Fact]
    public async Task ReplacementKeyBlockWinsActivationAndSupportsNextExactReattestation()
    {
        var f = new DeletionConsumptionFixture(); var request = DeletionConsumptionFixture.Request(); await f.Actor.RegisterAsync(request);
        await f.Actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation()); var old = await f.Actor.LookupAsync("tenant-a", "batch-1");
        await f.Actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation("key-v2"));
        var activation = DeletionConsumptionFixture.Activation(old, DeletionConsumptionFixture.Request(version: "key-v2", attestation: 2));
        var blocked = await f.Actor.ActivateAsync(activation); blocked.Status.ShouldBe(DeletionConsumptionStatus.ActivationBlockedByReplacementKeyCompromise);
        blocked.BlockedKeyVersion.ShouldBe("key-v2"); blocked.RevocationRevision.ShouldBe(1); JsonSerializer.Serialize(await f.Actor.ActivateAsync(activation)).ShouldBe(JsonSerializer.Serialize(blocked));
        var persisted = await f.Actor.LookupAsync("tenant-a", "batch-1"); persisted.Status.ShouldBe(DeletionConsumptionStatus.ConsumptionBlocked); persisted.ReceiptId.ShouldBe(blocked.ReceiptId);
        var third = DeletionConsumptionFixture.Activation(persisted, DeletionConsumptionFixture.Request(version: "key-v3", attestation: 3), "activate-2");
        (await f.Actor.ActivateAsync(third)).Status.ShouldBe(DeletionConsumptionStatus.Unconsumed);
        f.Backend.CommittedState.Single().Value.ShouldBeOfType<DeletionConsumptionLedger>().Revocations.Count.ShouldBe(2);
        await f.Provider.DidNotReceive().ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
    /// <summary>Unrelated changed block-set compare fails without mutation; a later replacement-key revocation blocks an activated unreserved batch.</summary>
    [Fact]
    public async Task ActivationCompareIsAtomicAndPostActivationCompromiseStillBlocksReserve()
    {
        var f = new DeletionConsumptionFixture(); var request = DeletionConsumptionFixture.Request(); await f.Actor.RegisterAsync(request);
        await f.Actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation()); var old = await f.Actor.LookupAsync("tenant-a", "batch-1");
        await f.Actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation("unrelated"));
        var activation = DeletionConsumptionFixture.Activation(old, DeletionConsumptionFixture.Request(version: "key-v2", attestation: 2));
        (await f.Actor.ActivateAsync(activation)).Status.ShouldBe(DeletionConsumptionStatus.Conflict);
        var corrected = activation with { ExpectedKeyBlockSetRevision = 2 };
        (await f.Actor.ActivateAsync(corrected)).Status.ShouldBe(DeletionConsumptionStatus.Unconsumed);
        (await f.Actor.ActivateAsync(corrected with { GuardReplacementReceiptId = "changed" })).Status.ShouldBe(DeletionConsumptionStatus.Conflict);
        await f.Actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation("key-v2"));
        (await f.Actor.ReserveAndConsumeAsync(corrected.Replacement)).Status.ShouldBe(DeletionConsumptionStatus.ConsumptionBlocked);
    }
    /// <summary>Admission integrity blocks never reopen and immutable target/attestation substitutions never become activation.</summary>
    [Fact]
    public async Task AdmissionIntegrityAndChangedTargetsCannotReopen()
    {
        var f = new DeletionConsumptionFixture(); var request = DeletionConsumptionFixture.Request(); await f.Actor.RegisterAsync(request);
        var blocked = await f.Actor.BlockAsync(new("tenant-a", "batch-1", "admission-1", "accepted-1"));
        (await f.Actor.ActivateAsync(DeletionConsumptionFixture.Activation(blocked, DeletionConsumptionFixture.Request(version: "key-v2", attestation: 2)))).Status.ShouldBe(DeletionConsumptionStatus.Conflict);
        (await f.Actor.RegisterAsync(request with { Capability = request.Capability with { DestructionSealId = "changed" } })).Status.ShouldBe(DeletionConsumptionStatus.Conflict);
    }
    /// <summary>Exact target lookup returns its original vector under another batch; overlapping reserved/mixed targets cannot start another manifest.</summary>
    [Fact]
    public async Task CompleteTargetIndexDefendsOriginalDestructionAndPendingReservation()
    {
        var f = new DeletionConsumptionFixture { LoseResponse = true }; var request = DeletionConsumptionFixture.Request(); await f.Actor.RegisterAsync(request);
        await f.Actor.ReserveAndConsumeAsync(request);
        (await f.Actor.RegisterAsync(DeletionConsumptionFixture.Request("batch-2"))).Status.ShouldBe(DeletionConsumptionStatus.AlreadyDestroyedByBatch);
        var consumed = await f.Actor.LookupAsync("tenant-a", "batch-1");
        var already = await f.Actor.RegisterAsync(DeletionConsumptionFixture.Request("batch-2")); already.Status.ShouldBe(DeletionConsumptionStatus.AlreadyDestroyedByBatch);
        already.TargetReceipts.ShouldBe(consumed.TargetReceipts); already.TargetReceipts.All(r => r.OriginalBatchId == "batch-1").ShouldBeTrue();
        var mixed = DeletionConsumptionFixture.Request("batch-3", targets: new[] { request.Targets[0], new ProtectionTarget("tenant-a", "interaction-c", "alias-c") });
        (await f.Actor.RegisterAsync(mixed)).Status.ShouldBe(DeletionConsumptionStatus.Conflict);
        var distinct = DeletionConsumptionFixture.Request("batch-4", targets: new[] { new ProtectionTarget("tenant-a", "interaction-a", "alias-other") });
        (await f.Actor.RegisterAsync(distinct)).Status.ShouldBe(DeletionConsumptionStatus.Unconsumed);
    }
    /// <summary>Exact duplicates survive serialization/restart, while changed/cross-tenant revocation evidence rejects.</summary>
    [Fact]
    public async Task RevocationLookupAndNonExpiringConsumedResultSurviveRestart()
    {
        var f = new DeletionConsumptionFixture(); var request = DeletionConsumptionFixture.Request(); await f.Actor.RegisterAsync(request);
        var consumed = await f.Actor.ReserveAndConsumeAsync(request); var envelope = DeletionConsumptionFixture.Revocation(); var receipt = await f.Actor.RegisterRevocationAsync(envelope);
        var saved = f.Backend.CommittedState.Single(); var restored = new InMemoryStateManager();
        await restored.SetStateAsync(saved.Key, JsonSerializer.Deserialize<DeletionConsumptionLedger>(JsonSerializer.Serialize(saved.Value))!, TestContext.Current.CancellationToken);
        await restored.SaveStateAsync(TestContext.Current.CancellationToken); var actor = DeletionConsumptionFixture.Create(restored, f.Authority);
        var lookup = await actor.LookupAsync("tenant-a", "batch-1"); lookup.Status.ShouldBe(DeletionConsumptionStatus.Consumed); lookup.ReceiptId.ShouldBe(consumed.ReceiptId); lookup.TargetReceipts.ShouldBe(consumed.TargetReceipts);
        (await actor.LookupRevocationAsync(envelope))!.ReceiptId.ShouldBe(receipt!.ReceiptId);
        await Should.ThrowAsync<ArgumentException>(() => actor.LookupRevocationAsync(envelope with { SignatureDigest = new string('B', 64) }));
        await Should.ThrowAsync<ArgumentException>(() => actor.RegisterRevocationAsync(envelope with { TenantId = "tenant-b" }));
        JsonSerializer.Serialize(saved.Value).ShouldNotContain("ExpiresAt");
    }
    /// <summary>Missing or denied independent authority/provider retains no batch and performs no physical call.</summary>
    [Fact]
    public async Task MissingOrDeniedQualificationFailsClosed()
    {
        var f = new DeletionConsumptionFixture(); var request = DeletionConsumptionFixture.Request();
        (await DeletionConsumptionFixture.Create(f.Backend).RegisterAsync(request)).Status.ShouldBe(DeletionConsumptionStatus.Unavailable);
        f.Authority.VerifyDispatchAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<CancellationToken>()).Returns(false);
        (await f.Actor.RegisterAsync(request)).Status.ShouldBe(DeletionConsumptionStatus.Unavailable); f.Backend.CommittedState.ShouldBeEmpty();
        await f.Provider.DidNotReceive().ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
    /// <summary>Precommit failure or committed lost acknowledgement never sends destruction from unconfirmed staged cache.</summary>
    [Theory]
    [InlineData(1, false)][InlineData(1, true)][InlineData(2, false)][InlineData(2, true)]
    public async Task ReservationSaveFailureUsesPersistedStateOnly(int failSave, bool committed)
    {
        var f = new DeletionConsumptionFixture(); var request = DeletionConsumptionFixture.Request(); await f.Actor.RegisterAsync(request);
        var manager = DeletionConsumptionFixture.Faulting<DeletionConsumptionLedger>(f.Backend, failSave, committed);
        await Should.ThrowAsync<HttpRequestException>(() => DeletionConsumptionFixture.Create(manager, f.Authority, f.Provider).ReserveAndConsumeAsync(request));
        f.Anchor.ShouldBe(failSave == 1 ? 1 : 2);
        await f.Provider.DidNotReceive().ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        foreach (var item in f.Backend.CommittedState.ToArray())
        {
            if (item.Value is AnchoredStateTransition pending) { await f.Backend.SetStateAsync(item.Key, JsonSerializer.Deserialize<AnchoredStateTransition>(JsonSerializer.Serialize(pending))!); }
            else { await f.Backend.SetStateAsync(item.Key, JsonSerializer.Deserialize<DeletionConsumptionLedger>(JsonSerializer.Serialize(item.Value))!); }
        }
        await f.Backend.SaveStateAsync();
        (await DeletionConsumptionFixture.Create(f.Backend, f.Authority, f.Provider).ReserveAndConsumeAsync(request)).Status.ShouldBe(DeletionConsumptionStatus.Consumed);
        var original = f.Backend.CommittedState.Single().Value.ShouldBeOfType<DeletionConsumptionLedger>().Batches.Single().Outcome;
        f.Retained.Keys.Single().ShouldNotBeNullOrWhiteSpace(); original.TargetReceipts.Count.ShouldBe(2);
        JsonSerializer.Serialize(await f.Actor.ReserveAndConsumeAsync(request)).ShouldBe(JsonSerializer.Serialize(original));
        await f.Provider.Received(1).ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Admission integrity remains permanently restrictive regardless of earlier/later compromise, including serialized restart.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdmissionIntegrityNeverReopensRegardlessOfCompromiseOrdering(bool compromiseFirst)
    {
        var f = new DeletionConsumptionFixture(); var request = DeletionConsumptionFixture.Request(); await f.Actor.RegisterAsync(request);
        if (compromiseFirst) { await f.Actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation()); }
        await f.Actor.BlockAsync(new("tenant-a", "batch-1", "admission-1", "accepted-1"));
        if (!compromiseFirst) { await f.Actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation()); }
        var saved = f.Backend.CommittedState.Single(); var restored = new InMemoryStateManager();
        await restored.SetStateAsync(saved.Key, JsonSerializer.Deserialize<DeletionConsumptionLedger>(JsonSerializer.Serialize(saved.Value))!, TestContext.Current.CancellationToken);
        await restored.SaveStateAsync(TestContext.Current.CancellationToken); var actor = DeletionConsumptionFixture.Create(restored, f.Authority, f.Provider);
        var blocked = await actor.LookupAsync("tenant-a", "batch-1"); blocked.BlockReason.ShouldBe(DeletionConsumptionBlockReason.AdmissionIntegrity);
        (await actor.ActivateAsync(DeletionConsumptionFixture.Activation(blocked, DeletionConsumptionFixture.Request(version: "key-v2", attestation: 2)))).Status.ShouldBe(DeletionConsumptionStatus.Conflict);
        (await actor.ReserveAndConsumeAsync(request)).BlockReason.ShouldBe(DeletionConsumptionBlockReason.AdmissionIntegrity);
        await f.Provider.DidNotReceive().ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
    /// <summary>Another batch for the same target recovers the original unknown reservation and cannot invoke a second physical request.</summary>
    [Fact]
    public async Task DifferentBatchCannotEscapeOriginalUnknownTargetReservation()
    {
        var f = new DeletionConsumptionFixture { LoseResponse = true }; var request = DeletionConsumptionFixture.Request(); await f.Actor.RegisterAsync(request);
        var reserved = await f.Actor.ReserveAndConsumeAsync(request);
        f.Provider.LookupAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
            new DeletionManifestProviderResult("tenant-a", "batch-1", call.Arg<string>(), DeletionManifestProviderState.Unknown, []));
        var unknown = await f.Actor.RegisterAsync(DeletionConsumptionFixture.Request("batch-2"));
        unknown.Status.ShouldBe(DeletionConsumptionStatus.Unavailable); unknown.BatchId.ShouldBe("batch-2"); unknown.ReceiptId.ShouldBeNull();
        f.Backend.CommittedState.Single().Value.ShouldBeOfType<DeletionConsumptionLedger>().Batches.Count.ShouldBe(1);
        await f.Provider.Received(1).ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), reserved.ReceiptId!, Arg.Any<CancellationToken>());
        f.Provider.LookupAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(f.Retained[reserved.ReceiptId!]);
        var recovered = await f.Actor.RegisterAsync(DeletionConsumptionFixture.Request("batch-2")); recovered.Status.ShouldBe(DeletionConsumptionStatus.AlreadyDestroyedByBatch);
        recovered.TargetReceipts.All(r => r.OriginalBatchId == "batch-1").ShouldBeTrue();
        await f.Provider.Received(1).ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), reserved.ReceiptId!, Arg.Any<CancellationToken>());
    }

    /// <summary>A blocked admission releases the original turn at its entry deadline and cannot later register a batch.</summary>
    [Fact]
    public async Task SuspendedAdmissionReleasesWholeEntryWithoutLateRegistration()
    {
        var f = new DeletionConsumptionFixture(); var clock = new RetainedHistoryTimeProvider(DateTimeOffset.UtcNow);
        var actor = DeletionConsumptionFixture.Create(f.Backend, f.Authority, f.Provider, clock);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Authority.AuthorizeOperationAsync("tenant-a", "batch-1", "RegisterDeletionBatch", Arg.Any<CancellationToken>()).Returns(_ =>
        { entered.TrySetResult(); return release.Task; });
        var pending = actor.RegisterAsync(DeletionConsumptionFixture.Request());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(30));
        (await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Status.ShouldBe(DeletionConsumptionStatus.Unavailable);
        (await actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation("other-key"))).ShouldNotBeNull();
        string retained = JsonSerializer.Serialize(f.Backend.CommittedState);
        release.SetResult(true);
        JsonSerializer.Serialize(f.Backend.CommittedState).ShouldBe(retained);
        await f.Provider.DidNotReceive().ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Stalled independent admission and journal record calls receive the private deadline token and cannot apply a late actor state effect.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task SuspendedTransitionJournalUsesEntryDeadlineWithoutLateActorEffect(bool record)
    {
        var fixture = new DeletionConsumptionFixture(); var clock = new RetainedHistoryTimeProvider(DateTimeOffset.UtcNow);
        var actor = DeletionConsumptionFixture.Create(fixture.Backend, fixture.Authority, fixture.Provider, clock);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken supplied = default;
        if (record)
        {
            fixture.Authority.RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call =>
            { supplied = call.Arg<CancellationToken>(); entered.TrySetResult(); return release.Task; });
        }
        else
        {
            fixture.Authority.AdmitTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call =>
            { supplied = call.Arg<CancellationToken>(); entered.TrySetResult(); return release.Task; });
        }
        var pending = actor.RegisterAsync(DeletionConsumptionFixture.Request());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        supplied.CanBeCanceled.ShouldBeTrue();
        clock.Advance(TimeSpan.FromSeconds(30));
        (await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Status.ShouldBe(DeletionConsumptionStatus.Unavailable);
        supplied.IsCancellationRequested.ShouldBeTrue();
        string retained = JsonSerializer.Serialize(fixture.Backend.CommittedState);
        release.SetResult(true);
        await Task.Yield();
        JsonSerializer.Serialize(fixture.Backend.CommittedState).ShouldBe(retained);
        await fixture.Provider.DidNotReceive().ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A state task remains turn-owned after deadline release; later entries wait for its actual completion.</summary>
    [Fact]
    public async Task SuspendedStateReadExcludesLaterTurnUntilCompletion()
    {
        var f = new DeletionConsumptionFixture(); var clock = new RetainedHistoryTimeProvider(DateTimeOffset.UtcNow);
        var manager = Substitute.For<IActorStateManager>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); int clears = 0;
        manager.ClearCacheAsync(Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (Interlocked.Increment(ref clears) == 1) { entered.TrySetResult(); return release.Task; }
            return f.Backend.ClearCacheAsync(call.Arg<CancellationToken>());
        });
        manager.TryGetStateAsync<DeletionConsumptionLedger>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => f.Backend.TryGetStateAsync<DeletionConsumptionLedger>(call.Arg<string>(), call.Arg<CancellationToken>()));
        manager.TryGetStateAsync<AnchoredStateTransition>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => f.Backend.TryGetStateAsync<AnchoredStateTransition>(call.Arg<string>(), call.Arg<CancellationToken>()));
        manager.SetStateAsync(Arg.Any<string>(), Arg.Any<DeletionConsumptionLedger>(), Arg.Any<CancellationToken>()).Returns(call => f.Backend.SetStateAsync(call.Arg<string>(), call.Arg<DeletionConsumptionLedger>(), call.Arg<CancellationToken>()));
        manager.SetStateAsync(Arg.Any<string>(), Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call => f.Backend.SetStateAsync(call.Arg<string>(), call.Arg<AnchoredStateTransition>(), call.Arg<CancellationToken>()));
        manager.TryRemoveStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => f.Backend.TryRemoveStateAsync(call.Arg<string>(), call.Arg<CancellationToken>()));
        manager.SaveStateAsync(Arg.Any<CancellationToken>()).Returns(call => f.Backend.SaveStateAsync(call.Arg<CancellationToken>()));
        var actor = DeletionConsumptionFixture.Create(manager, f.Authority, f.Provider, clock);
        var pending = actor.LookupAsync("tenant-a", "batch-1");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(30));
        (await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Status.ShouldBe(DeletionConsumptionStatus.Unavailable);
        (await actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation("other-key"))).ShouldBeNull();
        f.Backend.CommittedState.ShouldBeEmpty(); release.SetResult();
        (await actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation("other-key"))).ShouldNotBeNull();
        f.Backend.CommittedState.Single().Value.ShouldBeOfType<DeletionConsumptionLedger>().Revocations.Count.ShouldBe(1);
    }

    /// <summary>Consumed/blocked receipts remain immutable while missing or withdrawn current exact private lookup credentials deny release.</summary>
    [Fact]
    public async Task ImmutableOwnerOutcomeDoesNotGrantReadAuthority()
    {
        var f = new DeletionConsumptionFixture(); var request = DeletionConsumptionFixture.Request(); await f.Actor.RegisterAsync(request);
        var consumed = await f.Actor.ReserveAndConsumeAsync(request);
        (await DeletionConsumptionFixture.Create(f.Backend).LookupAsync("tenant-a", "batch-1")).Status.ShouldBe(DeletionConsumptionStatus.Unavailable);
        f.Authority.AuthorizeOperationAsync("tenant-a", "batch-1", "LookupDeletionBatch", Arg.Any<CancellationToken>()).Returns(false);
        (await f.Actor.LookupAsync("tenant-a", "batch-1")).Status.ShouldBe(DeletionConsumptionStatus.Unavailable);
        JsonSerializer.Serialize(f.Backend.CommittedState.Single().Value.ShouldBeOfType<DeletionConsumptionLedger>().Batches.Single().Outcome).ShouldBe(JsonSerializer.Serialize(consumed));
        await f.Provider.Received(1).ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
    /// <summary>At each existing operational collection bound, a new append denies before persistence and prior immutable terminal evidence remains readable.</summary>
    [Theory]
    [InlineData("batches")]
    [InlineData("operations")]
    [InlineData("revocations")]
    public async Task AtCollectionBoundDoesNotPersistUnreadableState(string collection)
    {
        var f = new DeletionConsumptionFixture(); var original = DeletionConsumptionFixture.Request();
        await f.Actor.RegisterAsync(original); var consumed = await f.Actor.ReserveAndConsumeAsync(original);
        var saved = f.Backend.CommittedState.Single(); var state = (DeletionConsumptionLedger)saved.Value;
        if (collection == "batches")
        {
            var batches = state.Batches.ToList();
            for (int n = 1; n < 1000; n++)
            {
                var request = DeletionConsumptionFixture.Request("extra-" + n, targets: new[] { new ProtectionTarget("tenant-a", "other-" + n, "alias-" + n) });
                batches.Add(new(request, request, new("tenant-a", request.Capability.BatchId, DeletionConsumptionStatus.Unconsumed, n + 3, 0, "registration-" + n, null, null, null, [])));
            }
            state = state with { Revision = 1002, Batches = batches.ToArray() };
        }
        else if (collection == "operations")
        {
            state = state with { Revision = 10002, Operations = Enumerable.Range(0, 10000).Select(n => new DeletionConsumptionOperation("prior-op-" + n, new string('A', 64), consumed)).ToArray() };
        }
        else
        {
            state = state with { Revision = 10002, KeyBlockSetRevision = 10000, Revocations = Enumerable.Range(1, 10000).Select(n => {
                var envelope = DeletionConsumptionFixture.Revocation(revision: n);
                return new DeletionCapabilityRevocationReceipt(envelope, n + 2, n, DeletionConsumptionIdentity.Digest(envelope), []);
            }).ToArray() };
        }
        await f.Backend.SetStateAsync(saved.Key, state, TestContext.Current.CancellationToken); await f.Backend.SaveStateAsync(TestContext.Current.CancellationToken);
        f.Anchor = state.Revision; f.AnchorDigest = DeletionConsumptionIdentity.Digest(state); // Synthetic independent capacity installation, not restore authorization.
        string before = DeletionConsumptionIdentity.Digest(f.Backend.CommittedState.Single().Value);
        if (collection == "batches") { (await f.Actor.RegisterAsync(DeletionConsumptionFixture.Request("capacity-new", targets: new[] { new ProtectionTarget("tenant-a", "new", "new") }))).Status.ShouldBe(DeletionConsumptionStatus.Unavailable); }
        else if (collection == "operations") { (await f.Actor.BlockAsync(new("tenant-a", original.Capability.BatchId, "new-op", "admission-evidence"))).Status.ShouldBe(DeletionConsumptionStatus.Unavailable); }
        else { (await f.Actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation(revision: 10001))).ShouldBeNull(); }
        DeletionConsumptionIdentity.Digest(f.Backend.CommittedState.Single().Value).ShouldBe(before);
        DeletionConsumptionIdentity.Digest(await f.Actor.LookupAsync("tenant-a", original.Capability.BatchId)).ShouldBe(DeletionConsumptionIdentity.Digest(consumed));
        await f.Provider.Received(1).ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
    /// <summary>Older or equal-revision divergent restored admission state cannot reopen physical consumption; latest independent outcome remains readable.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestoredAdmissionStateCannotReopenConsumption(bool equalRevision)
    {
        var f = new DeletionConsumptionFixture(); var request = DeletionConsumptionFixture.Request(); await f.Actor.RegisterAsync(request);
        var original = JsonSerializer.Deserialize<DeletionConsumptionLedger>(JsonSerializer.Serialize(f.Backend.CommittedState.Single().Value))!;
        var blocked = await f.Actor.BlockAsync(new("tenant-a", "batch-1", "admission-restore", "accepted-restore"));
        var latest = f.Backend.CommittedState.Single(); var restored = new InMemoryStateManager();
        var divergent = equalRevision ? original with { Revision = ((DeletionConsumptionLedger)latest.Value).Revision } : original;
        await restored.SetStateAsync(latest.Key, divergent, TestContext.Current.CancellationToken); await restored.SaveStateAsync(TestContext.Current.CancellationToken);
        var actor = DeletionConsumptionFixture.Create(restored, f.Authority, f.Provider);
        await Should.ThrowAsync<InvalidOperationException>(() => actor.ReserveAndConsumeAsync(request));
        await f.Provider.DidNotReceive().ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await restored.SetStateAsync(latest.Key, JsonSerializer.Deserialize<DeletionConsumptionLedger>(JsonSerializer.Serialize(latest.Value))!, TestContext.Current.CancellationToken); await restored.SaveStateAsync(TestContext.Current.CancellationToken);
        DeletionConsumptionIdentity.Digest(await actor.LookupAsync("tenant-a", "batch-1")).ShouldBe(DeletionConsumptionIdentity.Digest(blocked));
    }


    /// <summary>A covered requesting batch retains its own nonempty aggregate receipt and original target proofs across either pending/main persistence failure, retry and serialized restart.</summary>
    [Theory]
    [InlineData(1, false)][InlineData(1, true)][InlineData(2, false)][InlineData(2, true)]
    public async Task AlreadyDestroyedRequestingBatchRetainsExactOriginalCoveredOutcome(int failSave, bool committed)
    {
        var f = new DeletionConsumptionFixture(); var original = DeletionConsumptionFixture.Request(); await f.Actor.RegisterAsync(original);
        var consumed = await f.Actor.ReserveAndConsumeAsync(original); var covered = DeletionConsumptionFixture.Request("covered-requesting-batch");
        var manager = DeletionConsumptionFixture.Faulting<DeletionConsumptionLedger>(f.Backend, failSave, committed);
        await Should.ThrowAsync<HttpRequestException>(() => DeletionConsumptionFixture.Create(manager, f.Authority, f.Provider).RegisterAsync(covered));
        await f.Backend.ClearCacheAsync(TestContext.Current.CancellationToken);
        var retained = await DeletionConsumptionFixture.Create(f.Backend, f.Authority, f.Provider).RegisterAsync(covered);
        retained.Status.ShouldBe(DeletionConsumptionStatus.AlreadyDestroyedByBatch); retained.BatchId.ShouldBe(covered.Capability.BatchId); retained.ReceiptId.ShouldNotBeNullOrWhiteSpace();
        retained.TargetReceipts.ShouldBe(consumed.TargetReceipts); retained.TargetReceipts.All(r => r.OriginalBatchId == original.Capability.BatchId).ShouldBeTrue();
        var saved = f.Backend.CommittedState.Single(); var restored = new InMemoryStateManager();
        await restored.SetStateAsync(saved.Key, JsonSerializer.Deserialize<DeletionConsumptionLedger>(JsonSerializer.Serialize(saved.Value))!, TestContext.Current.CancellationToken); await restored.SaveStateAsync(TestContext.Current.CancellationToken);
        var restarted = DeletionConsumptionFixture.Create(restored, f.Authority, f.Provider);
        JsonSerializer.Serialize(await restarted.LookupAsync("tenant-a", covered.Capability.BatchId)).ShouldBe(JsonSerializer.Serialize(retained));
        JsonSerializer.Serialize(await restarted.ReserveAndConsumeAsync(covered)).ShouldBe(JsonSerializer.Serialize(retained));
        await f.Provider.Received(1).ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        restored.CommittedState.Single().Value.ShouldBeOfType<DeletionConsumptionLedger>().Batches.Count.ShouldBe(2);
    }
    /// <summary>Both suspended invocation and noncooperative physical tasks release the actor turn on its own deadline; late completion cannot mutate state or cause a second consume.</summary>
    [Theory]
    [InlineData(false, false)][InlineData(false, true)][InlineData(true, false)][InlineData(true, true)]
    public async Task PhysicalRecoveryDeadlinePreservesReservationAndAllowsSameTenantProgress(bool lookup, bool synchronous)
    {
        var f = new DeletionConsumptionFixture(); var request = DeletionConsumptionFixture.Request();
        var clock = new RetainedHistoryTimeProvider(DateTimeOffset.UtcNow);
        var actor = DeletionConsumptionFixture.Create(f.Backend, f.Authority, f.Provider, clock);
        await actor.RegisterAsync(request);
        if (lookup) { f.LoseResponse = true; (await actor.ReserveAndConsumeAsync(request)).Status.ShouldBe(DeletionConsumptionStatus.ConsumptionReserved); }
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource<DeletionManifestProviderResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DeletionManifestProviderResult> Suspended()
        {
            entered.TrySetResult();
            if (synchronous) { var result = released.Task.GetAwaiter().GetResult(); completed.TrySetResult(); return Task.FromResult(result); }
            return Complete();
            async Task<DeletionManifestProviderResult> Complete() { var result = await released.Task; completed.TrySetResult(); return result; }
        }
        if (lookup) { f.Provider.LookupAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => Suspended()); }
        else { f.Provider.ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => Suspended()); }
        var pending = lookup ? actor.LookupAsync("tenant-a", request.Capability.BatchId) : actor.ReserveAndConsumeAsync(request);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(30));
        var original = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        original.Status.ShouldBe(DeletionConsumptionStatus.ConsumptionReserved);
        (await actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation("unrelated-key"))).ShouldNotBeNull();
        var other = DeletionConsumptionFixture.Request("other-batch", "healthy-other", targets: [new("tenant-a", "other-interaction", "other-alias")]);
        (await actor.RegisterAsync(other)).Status.ShouldBe(DeletionConsumptionStatus.Unconsumed);
        string beforeLate = JsonSerializer.Serialize(f.Backend.CommittedState.Single().Value);
        var exact = new DeletionManifestProviderResult("tenant-a", request.Capability.BatchId, original.ReceiptId!, DeletionManifestProviderState.Consumed,
            request.Targets.Select((target, index) => new DeletionTargetReceipt(target, request.Capability.BatchId, "late-original-" + index)).ToArray());
        f.Retained[original.ReceiptId!] = exact; released.SetResult(exact);
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        JsonSerializer.Serialize(f.Backend.CommittedState.Single().Value).ShouldBe(beforeLate);
        f.Provider.LookupAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(exact);
        var restarted = DeletionConsumptionFixture.Create(f.Backend, f.Authority, f.Provider, clock);
        var recovered = await restarted.LookupAsync("tenant-a", request.Capability.BatchId); recovered.Status.ShouldBe(DeletionConsumptionStatus.Consumed);
        recovered.ReceiptId.ShouldBe(original.ReceiptId); recovered.TargetReceipts.ShouldBe(exact.TargetReceipts);
        await f.Provider.Received(1).ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        (await restarted.LookupAsync("tenant-a", "other-batch")).Status.ShouldBe(DeletionConsumptionStatus.Unconsumed);
    }

    /// <summary>A completed physical provider task cannot retain the actor through its receipt Count/traversal; the original reserve wins and later exact lookup never repeats consumption.</summary>
    [Theory]
    [InlineData(false, false, false)][InlineData(false, true, false)][InlineData(true, false, false)][InlineData(true, true, false)][InlineData(true, false, true)]
    public async Task SuspendedPhysicalReceiptCaptureRetainsReservationAndAllowsTenantProgress(bool lookup, bool traversal, bool notStarted)
    {
        var f = new DeletionConsumptionFixture(); var request = DeletionConsumptionFixture.Request();
        var clock = new RetainedHistoryTimeProvider(DateTimeOffset.UtcNow); var actor = DeletionConsumptionFixture.Create(f.Backend, f.Authority, f.Provider, clock);
        await actor.RegisterAsync(request); if (lookup) { f.LoseResponse = true; (await actor.ReserveAndConsumeAsync(request)).Status.ShouldBe(DeletionConsumptionStatus.ConsumptionReserved); }
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        void Suspend() { entered.TrySetResult(); release.Wait(); released.TrySetResult(); }
        string? reservation = null; IReadOnlyList<DeletionTargetReceipt> exact = request.Targets.Select((target, index) => new DeletionTargetReceipt(target, request.Capability.BatchId, "retained-original-" + index)).ToArray();
        var supplied = NSubstitute.Substitute.For<IReadOnlyList<DeletionTargetReceipt>>();
        supplied.Count.Returns(_ => { if (!traversal) { Suspend(); } return notStarted ? 0 : exact.Count; });
        IEnumerable<DeletionTargetReceipt> Enumerate() { Suspend(); foreach (var receipt in exact) { yield return receipt; } }
        supplied.GetEnumerator().Returns(_ => Enumerate().GetEnumerator());
        DeletionManifestProviderResult ProviderResult(string id) { reservation = id; return new("tenant-a", request.Capability.BatchId, id, notStarted ? DeletionManifestProviderState.NotStarted : DeletionManifestProviderState.Consumed, supplied); }
        if (lookup) { f.Provider.LookupAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => ProviderResult(call.Arg<string>())); }
        else { f.Provider.ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => ProviderResult(call.Arg<string>())); }
        var pending = lookup ? actor.LookupAsync("tenant-a", request.Capability.BatchId) : actor.ReserveAndConsumeAsync(request);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); clock.Advance(TimeSpan.FromSeconds(30));
        var original = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); original.Status.ShouldBe(DeletionConsumptionStatus.ConsumptionReserved);
        original.ReceiptId.ShouldBe(reservation);
        (await actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation("unrelated"))).ShouldNotBeNull();
        var other = DeletionConsumptionFixture.Request("other", "healthy", targets: [new("tenant-a", "other", "other")]);
        (await actor.RegisterAsync(other)).Status.ShouldBe(DeletionConsumptionStatus.Unconsumed);
        string before = JsonSerializer.Serialize(f.Backend.CommittedState); release.Set(); await released.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        JsonSerializer.Serialize(f.Backend.CommittedState).ShouldBe(before);
        f.Provider.LookupAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new DeletionManifestProviderResult("tenant-a", request.Capability.BatchId, original.ReceiptId!, DeletionManifestProviderState.Consumed, exact));
        var recovered = await DeletionConsumptionFixture.Create(f.Backend, f.Authority, f.Provider, clock).LookupAsync("tenant-a", request.Capability.BatchId);
        recovered.Status.ShouldBe(DeletionConsumptionStatus.Consumed); recovered.ReceiptId.ShouldBe(original.ReceiptId); recovered.TargetReceipts.ShouldBe(exact);
        await f.Provider.Received(1).ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        (await actor.LookupAsync("tenant-a", "other")).Status.ShouldBe(DeletionConsumptionStatus.Unconsumed);
    }

    /// <summary>Raw ordinal alias order agrees with the guard even when JSON escaping differs; reversed/duplicate vectors fail before admission/effect and the exact original survives consumption lookup/restart.</summary>
    [Theory]
    [InlineData("sorted")][InlineData("reversed")][InlineData("duplicate")]
    public async Task CanonicalRawTargetOrderPreservesOriginalAcrossConsumeAndRestart(string vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        var plus = new ProtectionTarget("tenant-a", "interaction-a", "key+v1");
        var minus = plus with { TargetProtectionKeyAlias = "key-v1" };
        IReadOnlyList<ProtectionTarget> targets = vector switch { "reversed" => [minus, plus], "duplicate" => [plus, plus], _ => [plus, minus] };
        var f = new DeletionConsumptionFixture { LoseResponse = true }; var request = DeletionConsumptionFixture.Request(targets: targets);
        if (vector != "sorted")
        {
            await Should.ThrowAsync<ArgumentException>(() => f.Actor.RegisterAsync(request));
            await f.Authority.DidNotReceive().VerifyDispatchAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<CancellationToken>());
            await f.Authority.DidNotReceive().RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>());
            f.Provider.ReceivedCalls().ShouldBeEmpty(); f.Backend.CommittedState.ShouldBeEmpty(); return;
        }
        (await f.Actor.RegisterAsync(request)).Status.ShouldBe(DeletionConsumptionStatus.Unconsumed);
        var reserved = await f.Actor.ReserveAndConsumeAsync(request); reserved.Status.ShouldBe(DeletionConsumptionStatus.ConsumptionReserved);
        var saved = f.Backend.CommittedState.Single(); var persisted = saved.Value.ShouldBeOfType<DeletionConsumptionLedger>();
        DeletionConsumptionIdentity.Digest(persisted.Batches.Single().Original).ShouldBe(DeletionConsumptionIdentity.Digest(request));
        var restored = new Hexalith.EventStore.Testing.Fakes.InMemoryStateManager();
        await restored.SetStateAsync(saved.Key, JsonSerializer.Deserialize<DeletionConsumptionLedger>(JsonSerializer.Serialize(saved.Value))!); await restored.SaveStateAsync();
        var restarted = DeletionConsumptionFixture.Create(restored, f.Authority, f.Provider);
        var original = await restarted.LookupAsync("tenant-a", request.Capability.BatchId); original.Status.ShouldBe(DeletionConsumptionStatus.Consumed);
        original.ReceiptId.ShouldBe(reserved.ReceiptId); original.TargetReceipts.Select(r => r.Target).ShouldBe(targets);
        DeletionConsumptionIdentity.Digest(await restarted.ReserveAndConsumeAsync(request)).ShouldBe(DeletionConsumptionIdentity.Digest(original));
        var final = restored.CommittedState.Single().Value.ShouldBeOfType<DeletionConsumptionLedger>().Batches.Single();
        DeletionConsumptionIdentity.Digest(final.Original).ShouldBe(DeletionConsumptionIdentity.Digest(request));
        DeletionConsumptionIdentity.Digest(final.Outcome).ShouldBe(DeletionConsumptionIdentity.Digest(original));
        await f.Provider.Received(1).ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), reserved.ReceiptId!, Arg.Any<CancellationToken>());
    }
}
