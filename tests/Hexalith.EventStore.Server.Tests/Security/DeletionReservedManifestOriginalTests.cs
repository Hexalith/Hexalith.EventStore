using System.Text.Json;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.EventStore.Testing.Fakes;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Read-only retained reservation source checks with synthetic independent authority and backend; no installed qualification.</summary>
public sealed class DeletionReservedManifestOriginalTests
{
    /// <summary>Reserved and terminal originals survive serialized actor reconstruction without changing any durable byte or revising the retained outcome.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExactReservedAndConsumedOriginalsSurviveReconstruction(bool consumed)
    {
        var f = new DeletionConsumptionFixture { LoseResponse = !consumed };
        var request = DeletionConsumptionFixture.Request() with { DetachedJws = "complete.protected-header..unchanged-signature" };
        await f.Actor.RegisterAsync(request);
        var outcome = await f.Actor.ReserveAndConsumeAsync(request);
        outcome.Status.ShouldBe(consumed ? DeletionConsumptionStatus.Consumed : DeletionConsumptionStatus.ConsumptionReserved);
        var restored = await RestoreAsync(f.Backend);
        string before = DurableBytes(restored);
        var manager = ReadOnlyManager(restored);
        f.Provider.ClearReceivedCalls(); f.Authority.ClearReceivedCalls();
        var actor = DeletionConsumptionFixture.Create(manager, f.Authority, f.Provider);
        var result = (await actor.ReadReservedManifestOriginalAsync("tenant-a", request.Capability.BatchId, outcome.ReceiptId!)).ShouldNotBeNull();
        JsonSerializer.Serialize(result.Original).ShouldBe(JsonSerializer.Serialize(request));
        JsonSerializer.Serialize(result.Outcome).ShouldBe(JsonSerializer.Serialize(outcome));
        result.Original.Targets.ShouldBe(request.Targets);
        result.Outcome.OwnerRevision.ShouldBe(outcome.OwnerRevision);
        DurableBytes(restored).ShouldBe(before);
        await AssertReadOnlyAsync(f, manager);
    }

    /// <summary>An ordinary lookup would start the retained NotStarted reservation; this accessor does not invoke either physical port.</summary>
    [Fact]
    public async Task ReadDoesNotStartRecoveryThatOrdinaryLookupWouldConsume()
    {
        var f = new DeletionConsumptionFixture(); var request = DeletionConsumptionFixture.Request();
        await f.Actor.RegisterAsync(request);
        f.Provider.ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DeletionManifestProviderResult>(new HttpRequestException("Synthetic provider has not started.")));
        var reserved = await f.Actor.ReserveAndConsumeAsync(request);
        reserved.Status.ShouldBe(DeletionConsumptionStatus.ConsumptionReserved); f.Retained.ShouldBeEmpty();
        var physical = new DeletionManifestProviderResult("tenant-a", request.Capability.BatchId, reserved.ReceiptId!, DeletionManifestProviderState.Consumed,
            request.Targets.Select((target, index) => new DeletionTargetReceipt(target, request.Capability.BatchId, "physical-" + index)).ToArray());
        f.Provider.ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(physical);
        f.Provider.ClearReceivedCalls(); string before = DurableBytes(f.Backend);
        var restarted = DeletionConsumptionFixture.Create(f.Backend, f.Authority, f.Provider);
        (await restarted.ReadReservedManifestOriginalAsync("tenant-a", request.Capability.BatchId, reserved.ReceiptId!)).ShouldNotBeNull();
        f.Provider.ReceivedCalls().ShouldBeEmpty(); DurableBytes(f.Backend).ShouldBe(before);
        (await restarted.LookupAsync("tenant-a", request.Capability.BatchId)).Status.ShouldBe(DeletionConsumptionStatus.Consumed);
        await f.Provider.Received(1).LookupAsync(Arg.Any<DeletionBatchConsumptionRequest>(), reserved.ReceiptId!, Arg.Any<CancellationToken>());
        await f.Provider.Received(1).ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), reserved.ReceiptId!, Arg.Any<CancellationToken>());
    }

    /// <summary>The reserved request is the exact legitimately activated Current, including replacement signature and guard facts.</summary>
    [Fact]
    public async Task EarlierReattestationReturnsCurrentRequestWithoutReconstruction()
    {
        var f = new DeletionConsumptionFixture { LoseResponse = true }; var original = DeletionConsumptionFixture.Request();
        await f.Actor.RegisterAsync(original); await f.Actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation());
        var blocked = await f.Actor.LookupAsync("tenant-a", original.Capability.BatchId);
        var replacement = DeletionConsumptionFixture.Request(version: "key-v2", attestation: 2) with { DetachedJws = "replacement-exact-detached-jws", CommittedIssuedGuardRevision = 10 };
        (await f.Actor.ActivateAsync(DeletionConsumptionFixture.Activation(blocked, replacement))).Status.ShouldBe(DeletionConsumptionStatus.Unconsumed);
        var reserved = await f.Actor.ReserveAndConsumeAsync(replacement);
        var restored = await RestoreAsync(f.Backend); string before = DurableBytes(restored);
        f.Provider.ClearReceivedCalls(); f.Authority.ClearReceivedCalls();
        var manager = ReadOnlyManager(restored);
        var read = (await DeletionConsumptionFixture.Create(manager, f.Authority, f.Provider)
            .ReadReservedManifestOriginalAsync("tenant-a", original.Capability.BatchId, reserved.ReceiptId!)).ShouldNotBeNull();
        JsonSerializer.Serialize(read.Original).ShouldBe(JsonSerializer.Serialize(replacement));
        read.Original.Capability.ShouldNotBe(original.Capability);
        DurableBytes(restored).ShouldBe(before); await AssertReadOnlyAsync(f, manager);
    }

    /// <summary>Registration, blocks and coverage receipts are not actual reservations; missing or changed selections disclose no original.</summary>
    [Theory]
    [InlineData("unconsumed")]
    [InlineData("blocked")]
    [InlineData("covered")]
    [InlineData("missing-batch")]
    [InlineData("wrong-receipt")]
    [InlineData("registration-receipt")]
    [InlineData("missing-state")]
    public async Task NonreservationAndMismatchedSelectionReturnNoOriginal(string selection)
    {
        var f = new DeletionConsumptionFixture(); var request = DeletionConsumptionFixture.Request(); string receipt = "unknown-receipt";
        if (selection != "missing-state")
        {
            var outcome = await f.Actor.RegisterAsync(request);
            if (selection == "blocked") { outcome = await f.Actor.BlockAsync(new("tenant-a", request.Capability.BatchId, "block-1", "integrity-evidence")); }
            string registrationReceipt = outcome.ReceiptId!;
            if (selection is "covered" or "wrong-receipt" or "registration-receipt") { outcome = await f.Actor.ReserveAndConsumeAsync(request); }
            if (selection == "covered") { request = DeletionConsumptionFixture.Request("covered-batch"); outcome = await f.Actor.RegisterAsync(request); outcome.Status.ShouldBe(DeletionConsumptionStatus.AlreadyDestroyedByBatch); }
            receipt = selection switch { "wrong-receipt" => "wrong-receipt", "registration-receipt" => registrationReceipt, _ => outcome.ReceiptId! };
        }
        string batchId = selection == "missing-batch" ? "missing-batch" : request.Capability.BatchId;
        string before = DurableBytes(f.Backend); f.Provider.ClearReceivedCalls(); f.Authority.ClearReceivedCalls();
        var manager = ReadOnlyManager(f.Backend);
        (await DeletionConsumptionFixture.Create(manager, f.Authority, f.Provider).ReadReservedManifestOriginalAsync("tenant-a", batchId, receipt)).ShouldBeNull();
        DurableBytes(f.Backend).ShouldBe(before); await AssertReadOnlyAsync(f, manager);
    }

    /// <summary>Missing/revoked permission, divergent anchors and a changed head at recheck all refuse without effects.</summary>
    [Theory]
    [InlineData("absent")]
    [InlineData("before")]
    [InlineData("after")]
    [InlineData("anchor")]
    [InlineData("changed-head")]
    public async Task UncertainAuthorityCannotReleaseOriginal(string uncertainty)
    {
        var f = new DeletionConsumptionFixture { LoseResponse = true }; var request = DeletionConsumptionFixture.Request();
        await f.Actor.RegisterAsync(request); var reserved = await f.Actor.ReserveAndConsumeAsync(request);
        f.Provider.ClearReceivedCalls(); f.Authority.ClearReceivedCalls(); int authorizations = 0; int validations = 0;
        if (uncertainty is "before" or "after")
        {
            f.Authority.AuthorizeOperationAsync("tenant-a", request.Capability.BatchId, "ReadReservedDeletionManifestOriginal", Arg.Any<CancellationToken>())
                .Returns(_ => ++authorizations < (uncertainty == "before" ? 1 : 2));
        }
        if (uncertainty is "anchor" or "changed-head")
        {
            f.Authority.ValidateStateAsync("tenant-a", Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(_ => ++validations < (uncertainty == "anchor" ? 1 : 2));
        }
        string before = DurableBytes(f.Backend); var manager = ReadOnlyManager(f.Backend);
        (await DeletionConsumptionFixture.Create(manager, uncertainty == "absent" ? null : f.Authority, f.Provider)
            .ReadReservedManifestOriginalAsync("tenant-a", request.Capability.BatchId, reserved.ReceiptId!)).ShouldBeNull();
        DurableBytes(f.Backend).ShouldBe(before); await AssertReadOnlyAsync(f, manager);
    }

    /// <summary>Two individually anchored snapshots at different retained heads cannot release a reservation original.</summary>
    [Fact]
    public async Task ChangedRetainedHeadBetweenSnapshotsReturnsNoOriginal()
    {
        var f = new DeletionConsumptionFixture { LoseResponse = true }; var request = DeletionConsumptionFixture.Request();
        await f.Actor.RegisterAsync(request); var reserved = await f.Actor.ReserveAndConsumeAsync(request);
        var saved = f.Backend.CommittedState.Single(); var previous = saved.Value.ShouldBeOfType<DeletionConsumptionLedger>();
        var successor = previous with { Revision = previous.Revision + 1 }; var manager = ReadOnlyManager(f.Backend); int reads = 0;
        manager.TryGetStateAsync<DeletionConsumptionLedger>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            if (++reads == 2)
            {
                // Synthetic independent writer advances the current head between this reader's snapshots.
                await f.Backend.SetStateAsync(saved.Key, successor, TestContext.Current.CancellationToken);
                await f.Backend.SaveStateAsync(TestContext.Current.CancellationToken);
                f.Anchor = successor.Revision; f.AnchorDigest = DeletionConsumptionIdentity.Digest(successor);
            }
            return await f.Backend.TryGetStateAsync<DeletionConsumptionLedger>(call.Arg<string>(), call.Arg<CancellationToken>());
        });
        f.Provider.ClearReceivedCalls(); f.Authority.ClearReceivedCalls();
        (await DeletionConsumptionFixture.Create(manager, f.Authority, f.Provider)
            .ReadReservedManifestOriginalAsync("tenant-a", request.Capability.BatchId, reserved.ReceiptId!)).ShouldBeNull();
        reads.ShouldBe(2); JsonSerializer.Serialize(f.Backend.CommittedState.Single().Value).ShouldBe(JsonSerializer.Serialize(successor));
        await AssertReadOnlyAsync(f, manager);
    }

    /// <summary>Even independently committed pending transitions cannot be reconciled by this read.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingOriginalIsUnavailableWithoutReconciliation(bool independentlyCommitted)
    {
        var f = new DeletionConsumptionFixture { LoseResponse = true }; var request = DeletionConsumptionFixture.Request();
        await f.Actor.RegisterAsync(request); var reserved = await f.Actor.ReserveAndConsumeAsync(request);
        if (independentlyCommitted)
        {
            var failing = DeletionConsumptionFixture.Faulting<DeletionConsumptionLedger>(f.Backend, 2, false);
            await Should.ThrowAsync<HttpRequestException>(() => DeletionConsumptionFixture.Create(failing, f.Authority, f.Provider)
                .RegisterRevocationAsync(DeletionConsumptionFixture.Revocation("unrelated-key")));
        }
        else
        {
            f.JournalAvailable = false;
            await Should.ThrowAsync<InvalidOperationException>(() => f.Actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation("unrelated-key")));
        }
        await f.Backend.ClearCacheAsync(TestContext.Current.CancellationToken);
        f.Backend.CommittedState.Values.OfType<AnchoredStateTransition>().Count().ShouldBe(1);
        string before = DurableBytes(f.Backend); long anchor = f.Anchor;
        f.Provider.ClearReceivedCalls(); f.Authority.ClearReceivedCalls(); var manager = ReadOnlyManager(f.Backend);
        (await DeletionConsumptionFixture.Create(manager, f.Authority, f.Provider)
            .ReadReservedManifestOriginalAsync("tenant-a", request.Capability.BatchId, reserved.ReceiptId!)).ShouldBeNull();
        f.Anchor.ShouldBe(anchor); DurableBytes(f.Backend).ShouldBe(before); await AssertReadOnlyAsync(f, manager);
        await f.Authority.DidNotReceive().VerifyTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Output vectors are detached read-only carriers and cannot change the retained ledger or later reads.</summary>
    [Fact]
    public async Task ReturnedOriginalOwnsItsVectors()
    {
        var f = new DeletionConsumptionFixture(); var request = DeletionConsumptionFixture.Request(); await f.Actor.RegisterAsync(request);
        var consumed = await f.Actor.ReserveAndConsumeAsync(request); string before = DurableBytes(f.Backend);
        var read = (await f.Actor.ReadReservedManifestOriginalAsync("tenant-a", request.Capability.BatchId, consumed.ReceiptId!)).ShouldNotBeNull();
        var batch = f.Backend.CommittedState.Single().Value.ShouldBeOfType<DeletionConsumptionLedger>().Batches.Single();
        ReferenceEquals(read.Original.Targets, batch.Current.Targets).ShouldBeFalse();
        ReferenceEquals(read.Outcome.TargetReceipts, batch.Outcome.TargetReceipts).ShouldBeFalse();
        Should.Throw<NotSupportedException>(() => ((IList<ProtectionTarget>)read.Original.Targets)[0] = new("tenant-a", "changed", "changed"));
        Should.Throw<NotSupportedException>(() => ((IList<DeletionTargetReceipt>)read.Outcome.TargetReceipts).Clear());
        DurableBytes(f.Backend).ShouldBe(before);
        JsonSerializer.Serialize(await f.Actor.ReadReservedManifestOriginalAsync("tenant-a", request.Capability.BatchId, consumed.ReceiptId!)).ShouldBe(JsonSerializer.Serialize(read));
    }

    /// <summary>Both state and independent authority stalls obey the actor's one finite entry budget, including noncooperative late completion.</summary>
    [Theory]
    [InlineData("authorization")]
    [InlineData("state")]
    [InlineData("anchor")]
    public async Task StalledReadsReturnUnavailableWithinWholeEntryBudget(string phase)
    {
        var f = new DeletionConsumptionFixture { LoseResponse = true }; var request = DeletionConsumptionFixture.Request(); await f.Actor.RegisterAsync(request);
        var reserved = await f.Actor.ReserveAndConsumeAsync(request); var state = f.Backend.CommittedState.Single().Value.ShouldBeOfType<DeletionConsumptionLedger>();
        var clock = new RetainedHistoryTimeProvider(DateTimeOffset.UtcNow); var manager = ReadOnlyManager(f.Backend);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> StallAsync() { entered.TrySetResult(); return await release.Task; }
        if (phase == "authorization") { f.Authority.AuthorizeOperationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => StallAsync()); }
        if (phase == "anchor") { f.Authority.ValidateStateAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => StallAsync()); }
        if (phase == "state")
        {
            manager.TryGetStateAsync<DeletionConsumptionLedger>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async _ =>
            { await StallAsync(); return new ConditionalValue<DeletionConsumptionLedger>(true, state); });
        }
        f.Provider.ClearReceivedCalls(); f.Authority.ClearReceivedCalls(); string before = DurableBytes(f.Backend);
        var pending = DeletionConsumptionFixture.Create(manager, f.Authority, f.Provider, clock)
            .ReadReservedManifestOriginalAsync("tenant-a", request.Capability.BatchId, reserved.ReceiptId!);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        clock.LastDueTime.ShouldBe(TimeSpan.FromSeconds(30)); clock.Advance(TimeSpan.FromSeconds(30));
        try { (await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).ShouldBeNull(); }
        finally { release.TrySetResult(true); }
        DurableBytes(f.Backend).ShouldBe(before); await AssertReadOnlyAsync(f, manager);
    }

    /// <summary>Independent awaits share the same budget instead of each receiving a fresh timeout.</summary>
    [Fact]
    public async Task SnapshotRecheckDoesNotResetWholeCallDeadline()
    {
        var f = new DeletionConsumptionFixture { LoseResponse = true }; var request = DeletionConsumptionFixture.Request(); await f.Actor.RegisterAsync(request);
        var reserved = await f.Actor.ReserveAndConsumeAsync(request); var clock = new RetainedHistoryTimeProvider(DateTimeOffset.UtcNow);
        int validations = 0;
        f.Authority.ValidateStateAsync("tenant-a", Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ =>
        { clock.Advance(TimeSpan.FromSeconds(++validations == 1 ? 20 : 11)); return true; });
        f.Provider.ClearReceivedCalls(); f.Authority.ClearReceivedCalls(); string before = DurableBytes(f.Backend); var manager = ReadOnlyManager(f.Backend);
        (await DeletionConsumptionFixture.Create(manager, f.Authority, f.Provider, clock)
            .ReadReservedManifestOriginalAsync("tenant-a", request.Capability.BatchId, reserved.ReceiptId!)).ShouldBeNull();
        validations.ShouldBe(2); DurableBytes(f.Backend).ShouldBe(before); await AssertReadOnlyAsync(f, manager);
    }

    /// <summary>The private adapter preserves all selection fields and the canonical tenant actor address across reconstruction.</summary>
    [Fact]
    public async Task PrivateAdapterPreservesExactSelectionAndTenant()
    {
        var f = new DeletionConsumptionFixture { LoseResponse = true }; var request = DeletionConsumptionFixture.Request(); await f.Actor.RegisterAsync(request);
        var reserved = await f.Actor.ReserveAndConsumeAsync(request); var proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<IDeletionConsumptionActor>(Arg.Any<ActorId>(), Arg.Any<string>()).Returns(call =>
        {
            call.Arg<ActorId>().GetId().ShouldBe(DeletionConsumptionActor.GetActorId("tenant-a")); call.Arg<string>().ShouldBe(DeletionConsumptionActor.ActorTypeName);
            return DeletionConsumptionFixture.Create(f.Backend, f.Authority, f.Provider);
        });
        var adapter = new DaprDeletionProtectionOwner("tenant-a", proxies, TimeProvider.System); string before = DurableBytes(f.Backend); f.Provider.ClearReceivedCalls();
        var read = (await adapter.ReadReservedManifestOriginalAsync("tenant-a", request.Capability.BatchId, reserved.ReceiptId!, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        JsonSerializer.Serialize(read.Original).ShouldBe(JsonSerializer.Serialize(request));
        (await adapter.ReadReservedManifestOriginalAsync("tenant-a", request.Capability.BatchId, "changed-receipt", TestContext.Current.CancellationToken)).ShouldBeNull();
        await Should.ThrowAsync<ArgumentException>(() => adapter.ReadReservedManifestOriginalAsync("tenant-b", request.Capability.BatchId, reserved.ReceiptId!, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentException>(() => f.Actor.ReadReservedManifestOriginalAsync("tenant-b", request.Capability.BatchId, reserved.ReceiptId!));
        DurableBytes(f.Backend).ShouldBe(before); f.Provider.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Caller cancellation and the adapter deadline cannot disclose a result from a stalled private transport.</summary>
    [Theory]
    [InlineData("before")]
    [InlineData("during")]
    [InlineData("deadline")]
    public async Task PrivateAdapterBoundsAndCancellationRefuseLateOriginal(string stop)
    {
        var proxies = Substitute.For<IActorProxyFactory>(); var actor = Substitute.For<IDeletionConsumptionActor>();
        proxies.CreateActorProxy<IDeletionConsumptionActor>(Arg.Any<ActorId>(), Arg.Any<string>()).Returns(actor);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<DeletionReservedManifestOriginal?>(TaskCreationOptions.RunContinuationsAsynchronously);
        actor.ReadReservedManifestOriginalAsync("tenant-a", "batch-1", "reservation-1").Returns(_ => { entered.TrySetResult(); return release.Task; });
        var clock = new RetainedHistoryTimeProvider(DateTimeOffset.UtcNow); var adapter = new DaprDeletionProtectionOwner("tenant-a", proxies, clock);
        using var cancellation = new CancellationTokenSource(); if (stop == "before") { cancellation.Cancel(); }
        var pending = adapter.ReadReservedManifestOriginalAsync("tenant-a", "batch-1", "reservation-1", cancellation.Token);
        if (stop != "before")
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            if (stop == "during") { cancellation.Cancel(); } else { clock.Advance(TimeSpan.FromSeconds(30)); }
        }
        try { await Should.ThrowAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)); }
        finally { release.TrySetResult(null); }
        if (stop == "before") { proxies.ReceivedCalls().ShouldBeEmpty(); }
        actor.ReceivedCalls().All(call => call.GetMethodInfo().Name == nameof(IDeletionConsumptionActor.ReadReservedManifestOriginalAsync)).ShouldBeTrue();
    }

    /// <summary>Existing owner implementations that omit the new port remain unavailable without invoking their existing operations.</summary>
    [Fact]
    public async Task OmittedOwnerPortDefaultsToUnavailable()
    {
        IDeletionProtectionOwner owner = new ReservedManifestUnavailableOwner();
        (await owner.ReadReservedManifestOriginalAsync("tenant-a", "batch-1", "reservation-1", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    private static string DurableBytes(InMemoryStateManager backend) => JsonSerializer.Serialize(backend.CommittedState);

    private static async Task<InMemoryStateManager> RestoreAsync(InMemoryStateManager backend)
    {
        var restored = new InMemoryStateManager();
        foreach (var entry in backend.CommittedState)
        {
            await restored.SetStateAsync(entry.Key, JsonSerializer.Deserialize<DeletionConsumptionLedger>(JsonSerializer.Serialize(entry.Value))!, TestContext.Current.CancellationToken);
        }
        await restored.SaveStateAsync(TestContext.Current.CancellationToken); return restored;
    }

    private static IActorStateManager ReadOnlyManager(InMemoryStateManager backend)
    {
        var manager = Substitute.For<IActorStateManager>();
        manager.ClearCacheAsync(Arg.Any<CancellationToken>()).Returns(call => backend.ClearCacheAsync(call.Arg<CancellationToken>()));
        manager.TryGetStateAsync<DeletionConsumptionLedger>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => backend.TryGetStateAsync<DeletionConsumptionLedger>(call.Arg<string>(), call.Arg<CancellationToken>()));
        manager.TryGetStateAsync<AnchoredStateTransition>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => backend.TryGetStateAsync<AnchoredStateTransition>(call.Arg<string>(), call.Arg<CancellationToken>()));
        return manager;
    }

    private static async Task AssertReadOnlyAsync(DeletionConsumptionFixture fixture, IActorStateManager manager)
    {
        fixture.Provider.ReceivedCalls().ShouldBeEmpty();
        manager.ReceivedCalls().All(call => call.GetMethodInfo().Name is nameof(IActorStateManager.ClearCacheAsync) or nameof(IActorStateManager.TryGetStateAsync)).ShouldBeTrue();
        await fixture.Authority.DidNotReceive().AdmitTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>());
        await fixture.Authority.DidNotReceive().RecoverTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>());
        await fixture.Authority.DidNotReceive().RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>());
        await fixture.Authority.DidNotReceive().RecordRevisionAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await fixture.Authority.DidNotReceive().VerifyDispatchAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<CancellationToken>());
        await fixture.Authority.DidNotReceive().VerifyActivationAsync(Arg.Any<DeletionReattestationActivation>(), Arg.Any<CancellationToken>());
    }
}
