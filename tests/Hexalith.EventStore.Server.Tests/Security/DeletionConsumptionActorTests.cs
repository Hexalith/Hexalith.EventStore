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
    /// <summary>Authenticated block commits before reserve; no physical call occurs and exact retries retain the original receipt.</summary>
    [Fact]
    public async Task AdmissionBlockBeforeReservePersistsAndPreventsEffect()
    {
        var f = new DeletionConsumptionFixture(); var request = DeletionConsumptionFixture.Request(); await f.Actor.RegisterAsync(request);
        var block = new DeletionBatchBlockRequest("tenant-a", "batch-1", "admission-1", "accepted-evidence-1");
        var outcome = await f.Actor.BlockAsync(block); outcome.Status.ShouldBe(DeletionConsumptionStatus.ConsumptionBlocked);
        DeletionConsumptionIdentity.Digest(await f.Actor.ReserveAndConsumeAsync(request)).ShouldBe(DeletionConsumptionIdentity.Digest(outcome)); DeletionConsumptionIdentity.Digest(await f.Actor.BlockAsync(block)).ShouldBe(DeletionConsumptionIdentity.Digest(outcome));
        await f.Provider.DidNotReceive().ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        f.Backend.CommittedState.Single().Value.ShouldBeOfType<DeletionConsumptionLedger>().Batches.Single().Outcome.ShouldBeEquivalentTo(outcome);
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
        f.Backend.CommittedState.Single().Value.ShouldBeOfType<DeletionConsumptionLedger>().Batches.Single().Outcome.ShouldBeEquivalentTo(consumed);
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
    public async Task MalformedPhysicalVectorCannotBecomeConsumed(string vector)
    {
        var f = new DeletionConsumptionFixture(); f.AlterResult = result => vector switch {
            "partial" => result with { TargetReceipts = result.TargetReceipts.Take(1).ToArray() },
            "target" => result with { TargetReceipts = result.TargetReceipts.Select(r => r with { Target = r.Target with { TargetProtectionKeyAlias = "substitution" } }).ToArray() },
            "order" => result with { TargetReceipts = result.TargetReceipts.Reverse().ToArray() },
            "batch" => result with { BatchId = "wrong" }, _ => result with { ReservationReceiptId = "wrong" }
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
        blocked.BlockedKeyVersion.ShouldBe("key-v2"); blocked.RevocationRevision.ShouldBe(1); (await f.Actor.ActivateAsync(activation)).ShouldBe(blocked);
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
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReservationSaveFailureUsesPersistedStateOnly(bool commitBeforeFault)
    {
        var f = new DeletionConsumptionFixture(); var request = DeletionConsumptionFixture.Request(); await f.Actor.RegisterAsync(request);
        var manager = Substitute.For<IActorStateManager>();
        manager.ClearCacheAsync(Arg.Any<CancellationToken>()).Returns(call => f.Backend.ClearCacheAsync(call.Arg<CancellationToken>()));
        manager.TryGetStateAsync<DeletionConsumptionLedger>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => f.Backend.TryGetStateAsync<DeletionConsumptionLedger>(call.Arg<string>(), call.Arg<CancellationToken>()));
        manager.SetStateAsync(Arg.Any<string>(), Arg.Any<DeletionConsumptionLedger>(), Arg.Any<CancellationToken>()).Returns(call => f.Backend.SetStateAsync(call.Arg<string>(), call.Arg<DeletionConsumptionLedger>(), call.Arg<CancellationToken>()));
        manager.SaveStateAsync(Arg.Any<CancellationToken>()).Returns(async call => {
            if (commitBeforeFault) { await f.Backend.SaveStateAsync(call.Arg<CancellationToken>()).ConfigureAwait(false); } throw new HttpRequestException("Controlled reservation save failure.");
        });
        await Should.ThrowAsync<HttpRequestException>(() => DeletionConsumptionFixture.Create(manager, f.Authority, f.Provider).ReserveAndConsumeAsync(request));
        var persisted = f.Backend.CommittedState.Single().Value.ShouldBeOfType<DeletionConsumptionLedger>().Batches.Single();
        persisted.Outcome.Status.ShouldBe(commitBeforeFault ? DeletionConsumptionStatus.ConsumptionReserved : DeletionConsumptionStatus.Unconsumed);
        await f.Provider.DidNotReceive().ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        if (commitBeforeFault) { (await f.Actor.ReserveAndConsumeAsync(request)).Status.ShouldBe(DeletionConsumptionStatus.Consumed); }
        else { await Should.ThrowAsync<InvalidOperationException>(() => f.Actor.ReserveAndConsumeAsync(request)); }
        if (commitBeforeFault) { f.Retained.Keys.Single().ShouldBe(persisted.Outcome.ReceiptId); }
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
        var unknown = await f.Actor.RegisterAsync(DeletionConsumptionFixture.Request("batch-2")); DeletionConsumptionIdentity.Digest(unknown).ShouldBe(DeletionConsumptionIdentity.Digest(reserved));
        f.Backend.CommittedState.Single().Value.ShouldBeOfType<DeletionConsumptionLedger>().Batches.Count.ShouldBe(1);
        await f.Provider.Received(1).ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), reserved.ReceiptId!, Arg.Any<CancellationToken>());
        f.Provider.LookupAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(f.Retained[reserved.ReceiptId!]);
        var recovered = await f.Actor.RegisterAsync(DeletionConsumptionFixture.Request("batch-2")); recovered.Status.ShouldBe(DeletionConsumptionStatus.AlreadyDestroyedByBatch);
        recovered.TargetReceipts.All(r => r.OriginalBatchId == "batch-1").ShouldBeTrue();
        await f.Provider.Received(1).ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), reserved.ReceiptId!, Arg.Any<CancellationToken>());
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
        f.Backend.CommittedState.Single().Value.ShouldBeOfType<DeletionConsumptionLedger>().Batches.Single().Outcome.ShouldBeEquivalentTo(consumed);
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

}
