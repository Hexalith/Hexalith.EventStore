using System.Text.Json;
using System.Reflection;
using System.Runtime.Serialization;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.EventStore.Testing.Fakes;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Actual persisted operation originals, reconstructed actors and strictly read-only recovery queries; synthetic authority is component evidence only.</summary>
public sealed class DeletionConsumptionOriginalTests
{
    /// <summary>Later physical completion and late block results cannot replace original registration/reservation/block revisions.</summary>
    [Fact]
    public async Task OriginalOutcomesSurvivePhysicalCompletionRestartAndExactRetry()
    {
        var f = new DeletionConsumptionFixture { LoseResponse = true };
        var request = DeletionConsumptionFixture.Request();
        var registration = await f.Actor.RegisterAsync(request);
        var reservation = await f.Actor.ReserveAndConsumeAsync(request);
        var block = new DeletionBatchBlockRequest("tenant-a", "batch-1", "late-admission", "accepted-1");
        var blockOutcome = await f.Actor.BlockAsync(block);
        var consumed = await f.Actor.LookupAsync("tenant-a", "batch-1");
        consumed.Status.ShouldBe(DeletionConsumptionStatus.Consumed);
        consumed.OwnerRevision.ShouldBeGreaterThan(reservation.OwnerRevision);
        var state = Ledger(f);
        Same(state.Batches.Single().RegistrationOutcome, registration);
        Same(state.Batches.Single().ReservationOutcome, reservation);
        state.Operations.Single().AdmissionBlockOriginal.ShouldBe(block);
        Same(state.Operations.Single().Outcome, blockOutcome);

        var restored = await RestoreAsync(f.Backend);
        var manager = ReadOnlyManager(restored);
        var actor = DeletionConsumptionFixture.Create(manager, f.Authority, f.Provider);
        string before = JsonSerializer.Serialize(restored.CommittedState);
        f.Authority.ClearReceivedCalls(); f.Provider.ClearReceivedCalls();
        var registered = (await actor.ReadRegistrationOriginalAsync(request)).ShouldNotBeNull();
        var reserved = (await actor.ReadReservationOriginalAsync(request)).ShouldNotBeNull();
        var blocked = (await actor.ReadBlockOriginalAsync(block)).ShouldNotBeNull();
        Same(registered.Original, request); Same(registered.Outcome, registration);
        Same(reserved.Original, request); Same(reserved.Outcome, reservation);
        blocked.Original.ShouldBe(block); Same(blocked.Outcome, blockOutcome);
        Same(TransportCopy(registered), registered); Same(TransportCopy(reserved), reserved); Same(TransportCopy(blocked), blocked);
        reserved.Outcome.TargetReceipts.ShouldBeEmpty();
        Same(await actor.ReadReservationOriginalAsync(request), reserved);
        JsonSerializer.Serialize(restored.CommittedState).ShouldBe(before);
        AssertReadOnly(f, manager);
    }

    /// <summary>Both successful and replacement-blocked activation inputs remain exact after the batch advances again.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActivationOriginalSurvivesLaterKeyAndBatchMutations(bool replacementCompromised)
    {
        var f = new DeletionConsumptionFixture(); var request = DeletionConsumptionFixture.Request();
        var registration = await f.Actor.RegisterAsync(request);
        await f.Actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation());
        if (replacementCompromised) { await f.Actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation("key-v2")); }
        var blocked = await f.Actor.LookupAsync("tenant-a", "batch-1");
        var activation = DeletionConsumptionFixture.Activation(blocked, DeletionConsumptionFixture.Request(version: "key-v2", attestation: 2));
        var original = await f.Actor.ActivateAsync(activation);
        if (!replacementCompromised) { await f.Actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation("key-v2")); }
        var nextBlocked = await f.Actor.LookupAsync("tenant-a", "batch-1");
        var successor = DeletionConsumptionFixture.Activation(nextBlocked, DeletionConsumptionFixture.Request(version: "key-v3", attestation: 3), "activate-2");
        (await f.Actor.ActivateAsync(successor)).Status.ShouldBe(DeletionConsumptionStatus.Unconsumed);
        (await f.Actor.ReserveAndConsumeAsync(successor.Replacement)).Status.ShouldBe(DeletionConsumptionStatus.Consumed);
        var persisted = Ledger(f);
        Same(persisted.Operations.Single(o => o.OperationId == activation.OperationId).ActivationOriginal, activation);
        Same(persisted.Operations.Single(o => o.OperationId == activation.OperationId).Outcome, original);
        var manager = ReadOnlyManager(await RestoreAsync(f.Backend));
        var actor = DeletionConsumptionFixture.Create(manager, f.Authority, f.Provider);
        f.Authority.ClearReceivedCalls(); f.Provider.ClearReceivedCalls();
        var result = (await actor.ReadActivationOriginalAsync(activation)).ShouldNotBeNull();
        Same(result.Original, activation); Same(result.Outcome, original);
        Same(TransportCopy(result), result);
        Same((await actor.ReadRegistrationOriginalAsync(request)).ShouldNotBeNull().Outcome, registration);
        (await actor.ReadRegistrationOriginalAsync(successor.Replacement)).ShouldBeNull();
        (await actor.ReadReservationOriginalAsync(request)).ShouldBeNull();
        (await actor.ReadReservationOriginalAsync(successor.Replacement)).ShouldNotBeNull();
        Same(await actor.ReadActivationOriginalAsync(activation), result);
        AssertReadOnly(f, manager);
    }

    /// <summary>A later same-target registration retains its actual original coverage vector without inventing a reservation.</summary>
    [Fact]
    public async Task AlreadyDestroyedRegistrationReadsTheOriginalCoverageVector()
    {
        var f = new DeletionConsumptionFixture(); var first = DeletionConsumptionFixture.Request();
        await f.Actor.RegisterAsync(first); await f.Actor.ReserveAndConsumeAsync(first);
        var later = DeletionConsumptionFixture.Request("batch-2");
        var covered = await f.Actor.RegisterAsync(later);
        covered.Status.ShouldBe(DeletionConsumptionStatus.AlreadyDestroyedByBatch);
        f.Provider.ClearReceivedCalls();
        var result = (await f.Actor.ReadRegistrationOriginalAsync(later)).ShouldNotBeNull();
        Same(result.Original, later); Same(result.Outcome, covered);
        result.Outcome.TargetReceipts.Select(r => r.OriginalBatchId).Distinct().ShouldBe([first.Capability.BatchId]);
        (await f.Actor.ReadReservationOriginalAsync(later)).ShouldBeNull();
        Same(TransportCopy(result), result); f.Provider.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>New nullable journals do not alter historical serialization; old states without originals remain unavailable.</summary>
    [Fact]
    public async Task MissingHistoricalOriginalsDenyWithoutReconstructionOrHashMigration()
    {
        var f = new DeletionConsumptionFixture(); var request = DeletionConsumptionFixture.Request();
        await f.Actor.RegisterAsync(request);
        var block = new DeletionBatchBlockRequest("tenant-a", "batch-1", "block-1", "accepted-1");
        await f.Actor.BlockAsync(block);
        var legacy = Ledger(f) with
        {
            Batches = Ledger(f).Batches.Select(b => b with { RegistrationOutcome = null, ReservationOutcome = null }).ToArray(),
            Operations = Ledger(f).Operations.Select(o => o with { AdmissionBlockOriginal = null }).ToArray(),
        };
        string bytes = JsonSerializer.Serialize(legacy);
        bytes.ShouldNotContain("RegistrationOutcome"); bytes.ShouldNotContain("ReservationOutcome"); bytes.ShouldNotContain("AdmissionBlockOriginal");
        Same(JsonSerializer.Deserialize<DeletionConsumptionLedger>(bytes), legacy);
        await ReplaceStateAsync(f, legacy);
        f.Authority.ClearReceivedCalls(); f.Provider.ClearReceivedCalls();
        (await f.Actor.ReadRegistrationOriginalAsync(request)).ShouldBeNull();
        (await f.Actor.ReadReservationOriginalAsync(request)).ShouldBeNull();
        (await f.Actor.ReadBlockOriginalAsync(block)).ShouldBeNull();
        JsonSerializer.Serialize(f.Backend.CommittedState.Single().Value).ShouldBe(bytes);
        f.Provider.ReceivedCalls().ShouldBeEmpty();
        await f.Authority.DidNotReceive().RecoverTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Malformed but coherently re-anchored original slots cannot turn later state into a different historical receipt.</summary>
    [Theory]
    [InlineData("registration-receipt")]
    [InlineData("registration-revision")]
    [InlineData("registration-tenant")]
    [InlineData("registration-status")]
    [InlineData("reservation-receipt")]
    [InlineData("reservation-revision")]
    [InlineData("reservation-status")]
    [InlineData("block-original")]
    public async Task MalformedRetainedOriginalsCannotBeReleased(string defect)
    {
        var f = new DeletionConsumptionFixture(); var request = DeletionConsumptionFixture.Request();
        await f.Actor.RegisterAsync(request); await f.Actor.ReserveAndConsumeAsync(request);
        var block = new DeletionBatchBlockRequest("tenant-a", "batch-1", "block-1", "accepted-1");
        await f.Actor.BlockAsync(block);
        var state = Ledger(f); var batch = state.Batches.Single();
        var registration = batch.RegistrationOutcome!; var reservation = batch.ReservationOutcome!;
        batch = defect switch
        {
            "registration-receipt" => batch with { RegistrationOutcome = registration with { ReceiptId = "other" } },
            "registration-revision" => batch with { RegistrationOutcome = registration with { OwnerRevision = 0 } },
            "registration-tenant" => batch with { RegistrationOutcome = registration with { TenantId = "tenant-b" } },
            "registration-status" => batch with { RegistrationOutcome = registration with { Status = DeletionConsumptionStatus.Consumed } },
            "reservation-receipt" => batch with { ReservationOutcome = reservation with { ReceiptId = "other" } },
            "reservation-revision" => batch with { ReservationOutcome = reservation with { OwnerRevision = registration.OwnerRevision } },
            "reservation-status" => batch with { ReservationOutcome = reservation with { Status = DeletionConsumptionStatus.Consumed } },
            _ => batch,
        };
        state = state with { Batches = [batch], Operations = state.Operations.Select(o => defect == "block-original"
            ? o with { AdmissionBlockOriginal = block with { AdmissionEvidenceId = "other" } } : o).ToArray() };
        await ReplaceStateAsync(f, state);
        string before = JsonSerializer.Serialize(f.Backend.CommittedState);
        await Should.ThrowAsync<InvalidOperationException>(() => f.Actor.ReadRegistrationOriginalAsync(request));
        JsonSerializer.Serialize(f.Backend.CommittedState).ShouldBe(before);
    }

    /// <summary>Lost pending/main save acknowledgement retains one original; only an authorized mutation may settle an unfinished transition.</summary>
    [Theory]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task LostRegistrationAcknowledgementRecoversOnlyTheAdmittedOriginal(int save, bool committed)
    {
        var f = new DeletionConsumptionFixture(); var request = DeletionConsumptionFixture.Request();
        var fault = DeletionConsumptionFixture.Create(DeletionConsumptionFixture.Faulting<DeletionConsumptionLedger>(f.Backend, save, committed), f.Authority, f.Provider);
        await Should.ThrowAsync<HttpRequestException>(() => fault.RegisterAsync(request));
        string before = JsonSerializer.Serialize(f.Backend.CommittedState);
        var restarted = DeletionConsumptionFixture.Create(f.Backend, f.Authority, f.Provider);
        var read = await restarted.ReadRegistrationOriginalAsync(request);
        JsonSerializer.Serialize(f.Backend.CommittedState).ShouldBe(before);
        if (save == 2 && committed) { read.ShouldNotBeNull(); } else { read.ShouldBeNull(); }
        var retry = await restarted.RegisterAsync(request);
        var original = (await restarted.ReadRegistrationOriginalAsync(request)).ShouldNotBeNull();
        Same(original.Outcome, retry); Same(original.Original, request);
        Ledger(f).Batches.Count.ShouldBe(1); Ledger(f).Revision.ShouldBe(1);
        f.Provider.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Loss at the main-state save retains block/activation/reservation inputs with their actual receipts and no read-triggered effect.</summary>
    [Theory]
    [InlineData("reserve")]
    [InlineData("block")]
    [InlineData("activate")]
    public async Task LostOperationAcknowledgementPreservesItsOriginal(string operation)
    {
        var f = new DeletionConsumptionFixture(); var request = DeletionConsumptionFixture.Request();
        await f.Actor.RegisterAsync(request);
        var block = new DeletionBatchBlockRequest("tenant-a", "batch-1", "block-1", "accepted-1");
        DeletionReattestationActivation? activation = null;
        if (operation == "activate")
        {
            await f.Actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation());
            activation = DeletionConsumptionFixture.Activation(await f.Actor.LookupAsync("tenant-a", "batch-1"), DeletionConsumptionFixture.Request(version: "key-v2", attestation: 2));
        }
        var fault = DeletionConsumptionFixture.Create(DeletionConsumptionFixture.Faulting<DeletionConsumptionLedger>(f.Backend, 2, true), f.Authority, f.Provider);
        await Should.ThrowAsync<HttpRequestException>(() => operation switch
        {
            "reserve" => fault.ReserveAndConsumeAsync(request), "block" => fault.BlockAsync(block), _ => fault.ActivateAsync(activation!),
        });
        var manager = ReadOnlyManager(await RestoreAsync(f.Backend));
        var restarted = DeletionConsumptionFixture.Create(manager, f.Authority, f.Provider);
        f.Authority.ClearReceivedCalls(); f.Provider.ClearReceivedCalls();
        if (operation == "reserve")
        {
            var result = (await restarted.ReadReservationOriginalAsync(request)).ShouldNotBeNull();
            Same(result.Original, request); Same(result.Outcome, Ledger(f).Batches.Single().ReservationOutcome);
            result.Outcome.Status.ShouldBe(DeletionConsumptionStatus.ConsumptionReserved);
        }
        else if (operation == "block")
        {
            var result = (await restarted.ReadBlockOriginalAsync(block)).ShouldNotBeNull();
            Same(result.Original, block); Same(result.Outcome, Ledger(f).Operations.Single().Outcome);
        }
        else
        {
            var result = (await restarted.ReadActivationOriginalAsync(activation!)).ShouldNotBeNull();
            Same(result.Original, activation); Same(result.Outcome, Ledger(f).Operations.Single().Outcome);
        }
        AssertReadOnly(f, manager);
    }

    /// <summary>An omitted optional port cannot silently fall back to mutable lookup or a mutation.</summary>
    [Fact]
    public async Task ExistingOwnersWithoutOriginalPortsDenyEveryRead()
    {
        IDeletionProtectionOwner owner = new ReservedManifestUnavailableOwner();
        var request = DeletionConsumptionFixture.Request();
        var block = new DeletionBatchBlockRequest("tenant-a", "batch-1", "block-1", "accepted-1");
        var activation = new DeletionReattestationActivation("activate-1", "block-1", 1, "replacement-1", request);
        (await owner.ReadRegistrationOriginalAsync(request, TestContext.Current.CancellationToken)).ShouldBeNull();
        (await owner.ReadReservationOriginalAsync(request, TestContext.Current.CancellationToken)).ShouldBeNull();
        (await owner.ReadBlockOriginalAsync(block, TestContext.Current.CancellationToken)).ShouldBeNull();
        (await owner.ReadActivationOriginalAsync(activation, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>Every receipt-bearing request field constrains selection; coherent alternate manifests cannot select an original.</summary>
    [Theory]
    [InlineData("signature")]
    [InlineData("dispatch")]
    [InlineData("issued")]
    [InlineData("dispatch-revision")]
    [InlineData("key")]
    [InlineData("attestation")]
    [InlineData("attempt")]
    [InlineData("intended")]
    [InlineData("batch")]
    [InlineData("manifest")]
    public async Task ChangedBatchRequestCannotSelectEitherOriginal(string member)
    {
        var f = new DeletionConsumptionFixture(); var request = DeletionConsumptionFixture.Request();
        await f.Actor.RegisterAsync(request); await f.Actor.ReserveAndConsumeAsync(request);
        var changed = Change(request, member);
        f.Authority.ClearReceivedCalls(); f.Provider.ClearReceivedCalls();
        var manager = ReadOnlyManager(f.Backend); var actor = DeletionConsumptionFixture.Create(manager, f.Authority, f.Provider);
        (await actor.ReadRegistrationOriginalAsync(changed)).ShouldBeNull();
        (await actor.ReadReservationOriginalAsync(changed)).ShouldBeNull();
        AssertReadOnly(f, manager);
    }

    /// <summary>Caller-owned targets changing during authority work cannot substitute the captured original or mutate the returned retained manifest.</summary>
    [Fact]
    public async Task OriginalReadDetachesCallerTargetsBeforeAwaitingAuthority()
    {
        var f = new DeletionConsumptionFixture();
        var targets = DeletionConsumptionFixture.Request().Targets.ToArray();
        var request = DeletionConsumptionFixture.Request(targets: targets);
        await f.Actor.RegisterAsync(request);
        string original = JsonSerializer.Serialize(request);
        f.Authority.AuthorizeOperationAsync("tenant-a", "batch-1", "ReadDeletionRegistrationOriginal", Arg.Any<CancellationToken>()).Returns(_ =>
        {
            targets[0] = targets[0] with { TargetProtectionKeyAlias = "changed-during-await" }; return true;
        });
        var read = (await f.Actor.ReadRegistrationOriginalAsync(request)).ShouldNotBeNull();
        JsonSerializer.Serialize(read.Original).ShouldBe(original);
        Ledger(f).Batches.Single().Original.Targets[0].TargetProtectionKeyAlias.ShouldBe("alias-a");
        Should.Throw<NotSupportedException>(() => ((IList<ProtectionTarget>)read.Original.Targets)[0] = targets[0]);
    }

    /// <summary>Original activation and admission inputs cannot be selected by a reused operation id with substituted evidence.</summary>
    [Fact]
    public async Task ChangedBlockAndActivationIdentityDeniesWhileExactInputsRemainReadable()
    {
        var f = new DeletionConsumptionFixture(); var request = DeletionConsumptionFixture.Request();
        await f.Actor.RegisterAsync(request); await f.Actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation());
        var blocked = await f.Actor.LookupAsync("tenant-a", "batch-1");
        var activation = DeletionConsumptionFixture.Activation(blocked, DeletionConsumptionFixture.Request(version: "key-v2", attestation: 2));
        await f.Actor.ActivateAsync(activation);
        foreach (var changed in new[] { activation with { OperationId = "other" }, activation with { CompromiseBlockReceiptId = "other" },
            activation with { GuardReplacementReceiptId = "other" }, activation with { ExpectedKeyBlockSetRevision = activation.ExpectedKeyBlockSetRevision + 1 },
            activation with { Replacement = Change(activation.Replacement, "signature") }, activation with { Replacement = Change(activation.Replacement, "manifest") } })
        { (await f.Actor.ReadActivationOriginalAsync(changed)).ShouldBeNull(); }
        (await f.Actor.ReadActivationOriginalAsync(activation)).ShouldNotBeNull();
        var block = new DeletionBatchBlockRequest("tenant-a", "batch-1", "block-1", "accepted-1");
        await f.Actor.BlockAsync(block);
        foreach (var changed in new[] { block with { OperationId = "other" }, block with { BatchId = "other" }, block with { AdmissionEvidenceId = "other" } })
        { (await f.Actor.ReadBlockOriginalAsync(changed)).ShouldBeNull(); }
        (await f.Actor.ReadBlockOriginalAsync(block)).ShouldNotBeNull();
        await Should.ThrowAsync<ArgumentException>(() => f.Actor.ReadBlockOriginalAsync(block with { TenantId = "tenant-b" }));
    }

    /// <summary>Absent/freshly denied authority, a restored head and an unsettled pending slot disclose no original or new effect.</summary>
    [Theory]
    [InlineData("absent")]
    [InlineData("permission")]
    [InlineData("final-permission")]
    [InlineData("restored")]
    [InlineData("pending")]
    [InlineData("fault")]
    public async Task UnavailableCurrentAuthorityOrUnsettledStateCannotReleaseOriginal(string condition)
    {
        var f = new DeletionConsumptionFixture(); var request = DeletionConsumptionFixture.Request();
        await f.Actor.RegisterAsync(request);
        var prior = await RestoreAsync(f.Backend);
        await f.Actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation());
        var backend = condition == "restored" ? prior : f.Backend;
        if (condition == "pending")
        {
            await backend.SetStateAsync("deletion-consumption-v23-pending-transition-v1", new AnchoredStateTransition("scope", 2, 3, "a", "b", [1]));
            await backend.SaveStateAsync();
        }
        int calls = 0;
        f.Authority.AuthorizeOperationAsync("tenant-a", "batch-1", "ReadDeletionRegistrationOriginal", Arg.Any<CancellationToken>())
            .Returns(_ => condition switch { "permission" => false, "final-permission" => ++calls == 1, "fault" => throw new HttpRequestException("authority-unavailable"), _ => true });
        var manager = ReadOnlyManager(backend);
        var actor = DeletionConsumptionFixture.Create(manager, condition == "absent" ? null : f.Authority, f.Provider);
        string before = JsonSerializer.Serialize(backend.CommittedState);
        f.Authority.ClearReceivedCalls(); f.Provider.ClearReceivedCalls();
        if (condition == "fault") { await Should.ThrowAsync<HttpRequestException>(() => actor.ReadRegistrationOriginalAsync(request)); }
        else { (await actor.ReadRegistrationOriginalAsync(request)).ShouldBeNull(); }
        JsonSerializer.Serialize(backend.CommittedState).ShouldBe(before);
        AssertReadOnly(f, manager);
    }

    /// <summary>Bounded private transport invokes only original reads, preserves caller cancellation and rejects foreign tenant requests.</summary>
    [Fact]
    public async Task PrivateAdapterRoutesExactReadsAndPreservesCancellation()
    {
        var actor = Substitute.For<IDeletionConsumptionActor>(); var proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<IDeletionConsumptionActor>(Arg.Any<Dapr.Actors.ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(actor);
        var owner = new DaprDeletionProtectionOwner("tenant-a", proxies, TimeProvider.System);
        var request = DeletionConsumptionFixture.Request();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<DeletionConsumptionOriginal<DeletionBatchConsumptionRequest>?>(TaskCreationOptions.RunContinuationsAsynchronously);
        actor.ReadRegistrationOriginalAsync(Arg.Any<DeletionBatchConsumptionRequest>()).Returns(_ => { started.TrySetResult(); return release.Task; });
        using var cancellation = new CancellationTokenSource();
        var pending = owner.ReadRegistrationOriginalAsync(request, cancellation.Token);
        await started.Task.WaitAsync(TestContext.Current.CancellationToken); cancellation.Cancel();
        var error = await Should.ThrowAsync<OperationCanceledException>(() => pending);
        error.CancellationToken.ShouldBe(cancellation.Token); release.TrySetResult(null);
        await owner.ReadReservationOriginalAsync(request, TestContext.Current.CancellationToken);
        var block = new DeletionBatchBlockRequest("tenant-a", "batch-1", "block-1", "evidence-1");
        await owner.ReadBlockOriginalAsync(block, TestContext.Current.CancellationToken);
        var activation = new DeletionReattestationActivation("activate-1", "block-1", 1, "replacement-1", request);
        await owner.ReadActivationOriginalAsync(activation, TestContext.Current.CancellationToken);
        actor.ReceivedCalls().Select(call => call.GetMethodInfo().Name).ShouldBe(new[] { "ReadRegistrationOriginalAsync", "ReadReservationOriginalAsync", "ReadBlockOriginalAsync", "ReadActivationOriginalAsync" });
        await Should.ThrowAsync<ArgumentException>(() => owner.ReadBlockOriginalAsync(block with { TenantId = "tenant-b" }, TestContext.Current.CancellationToken));
    }

    private static DeletionBatchConsumptionRequest Change(DeletionBatchConsumptionRequest request, string member)
    {
        var targets = request.Targets.Select(t => t with { TargetProtectionKeyAlias = t.TargetProtectionKeyAlias + "-changed" }).ToArray();
        return member switch
        {
            "signature" => request with { DetachedJws = "changed" }, "dispatch" => request with { DispatchReceiptId = "changed" },
            "issued" => request with { CommittedIssuedGuardRevision = request.CommittedIssuedGuardRevision + 1 },
            "dispatch-revision" => request with { DispatchGuardRevision = request.DispatchGuardRevision + 1 },
            "key" => request with { Capability = request.Capability with { CapabilityKeyVersion = "changed" } },
            "attestation" => request with { Capability = request.Capability with { AttestationOrdinal = request.Capability.AttestationOrdinal + 1 } },
            "attempt" => request with { Capability = request.Capability with { SigningAttemptOrdinal = request.Capability.SigningAttemptOrdinal + 1 } },
            "intended" => request with { Capability = request.Capability with { IntendedIssuedGuardRevision = request.Capability.IntendedIssuedGuardRevision + 1 } },
            "batch" => request with { Capability = request.Capability with { BatchId = "changed" } },
            _ => request with { Targets = targets, Capability = request.Capability with { ManifestDigest = DeletionConsumptionIdentity.TargetDigest(targets) } },
        };
    }
    private static void Same<T>(T actual, T expected) => JsonSerializer.Serialize(actual).ShouldBe(JsonSerializer.Serialize(expected));
    private static T TransportCopy<T>(T original)
    {
        var providerType = typeof(ActorProxy).Assembly.GetType("Dapr.Actors.Communication.ActorMessageBodyDataContractSerializationProvider", throwOnError: true)!;
        var provider = Activator.CreateInstance(providerType, nonPublic: true);
        var factory = providerType.GetMethod("CreateMessageBodyDataContractSerializer", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var serializer = (DataContractSerializer)factory.Invoke(provider, [typeof(T), new[] { typeof(T) }])!;
        using var stream = new MemoryStream(); serializer.WriteObject(stream, original); stream.Position = 0;
        return (T)serializer.ReadObject(stream)!;
    }
    private static DeletionConsumptionLedger Ledger(DeletionConsumptionFixture f) => f.Backend.CommittedState.Single().Value.ShouldBeOfType<DeletionConsumptionLedger>();
    private static async Task ReplaceStateAsync(DeletionConsumptionFixture f, DeletionConsumptionLedger state)
    {
        await f.Backend.SetStateAsync("deletion-consumption-v23", state, TestContext.Current.CancellationToken);
        await f.Backend.SaveStateAsync(TestContext.Current.CancellationToken); f.AnchorDigest = DeletionConsumptionIdentity.Digest(state);
    }
    private static async Task<InMemoryStateManager> RestoreAsync(InMemoryStateManager backend)
    {
        var restored = new InMemoryStateManager();
        foreach (var pair in backend.CommittedState)
        { await restored.SetStateAsync(pair.Key, JsonSerializer.Deserialize<DeletionConsumptionLedger>(JsonSerializer.Serialize(pair.Value))!, TestContext.Current.CancellationToken); }
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
    private static void AssertReadOnly(DeletionConsumptionFixture f, IActorStateManager manager)
    {
        f.Provider.ReceivedCalls().ShouldBeEmpty();
        manager.ReceivedCalls().All(call => call.GetMethodInfo().Name is nameof(IActorStateManager.ClearCacheAsync) or nameof(IActorStateManager.TryGetStateAsync)).ShouldBeTrue();
        f.Authority.ReceivedCalls().All(call => call.GetMethodInfo().Name is nameof(IDeletionConsumptionAuthority.AuthorizeOperationAsync) or nameof(IDeletionConsumptionAuthority.ValidateStateAsync)).ShouldBeTrue();
    }
}
