using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>Synthetic persisted-state proof for one source/target offboarding decision.</summary>
public sealed class TrustedEffectJointOffboardingTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Legal hold retains both partitions; authorized retry erases both before final purge.</summary>
    [Fact]
    public async Task LegalHoldAndAuthorizedOffboardingShareSourceTargetErasure()
    {
        var identity = new EffectIdentity(
            "test-tenant", "test-domain", "source-001", 7,
            EffectKindCatalog.DateResume, "test-domain", "agg-001", 0);
        string messageId = EffectIdentityCodec.ComputeMessageId(identity);
        var submission = new TrustedEffectSubmission(identity, "CreateOrder", [1, 2, 3], messageId, messageId);
        var context = new TrustedEffectContext("reactor", "date-resume", "source-cause", "synthetic-token");
        var admission = new TrustedEffectAdmission(submission, context, "SYNTHETIC-DIGEST");
        var sourceStore = new FaultInjectingActorStateManager();
        var targetStore = new FaultInjectingActorStateManager();
        var sourceIdentity = new AggregateIdentity(identity.Tenant, identity.SourceDomain, identity.SourceAggregate);
        string sourceEventKey = sourceIdentity.EventStreamKeyPrefix + identity.SourceEnvelopeSequence;
        await sourceStore.SeedCommittedStateAsync(new Dictionary<string, object>
        {
            [sourceIdentity.MetadataKey] = new AggregateMetadata(identity.SourceEnvelopeSequence, _now, null),
            [sourceEventKey] = new Hexalith.EventStore.Server.Events.EventEnvelope(
                "source-event", identity.SourceAggregate, "work-item", identity.Tenant,
                identity.SourceDomain, identity.SourceEnvelopeSequence, identity.SourceEnvelopeSequence,
                _now, "source-correlation", "source-cause", "synthetic-test", "v1",
                "SourceOccurred", 1, "json", [], null),
        });

        ITrustedEffectAdmissionPolicy admissionPolicy = Substitute.For<ITrustedEffectAdmissionPolicy>();
        using var keys = new StaticIdempotencyDigestKeyProvider(
            "test-v1", new Dictionary<string, byte[]>
            {
                ["test-v1"] = Enumerable.Repeat((byte)7, 32).ToArray(),
            }, []);
        ITrustedEffectErasureAuthority erasureAuthority = new TrustedEffectErasureCapability(
            keys, new FakeTimeProvider(_now));
        _ = admissionPolicy.PrepareAsync(submission, context, Arg.Any<CancellationToken>()).Returns(admission);
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        _ = invoker.InvokeAsync(
                Arg.Any<Hexalith.EventStore.Contracts.Commands.CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new AggregateActorTestHelper.TestEvent()]));
        ActorTestContext target = AggregateActorTestHelper.CreateActor(
            stateManager: targetStore,
            invoker: invoker,
            trustedEffectAdmissionPolicy: admissionPolicy,
            trustedEffectErasureAuthority: erasureAuthority);
        ActorTestContext source = AggregateActorTestHelper.CreateActor(
            stateManager: sourceStore,
            trustedEffectErasureAuthority: erasureAuthority,
            actorId: sourceIdentity.ActorId);
        TrustedEffectResult outcome = await target.Actor.ProcessTrustedEffectAsync(
            submission, context, "synthetic-proof");
        outcome.Disposition.ShouldBe(TrustedEffectDisposition.Success);
        string receiptKey = "effect_receipt_" + outcome.EffectId;
        sourceStore.CommittedState.Keys.ShouldContain(sourceEventKey);
        targetStore.CommittedState.Keys.ShouldContain(receiptKey);
        targetStore.CommittedState.Keys.ShouldContain(key => key.Contains(":events:1", StringComparison.Ordinal));
        _ = admissionPolicy.PrepareAsync(submission, context, Arg.Any<CancellationToken>())
            .Returns(admission with { SemanticDigest = "COLLIDING-SYNTHETIC-DIGEST" });
        await Should.ThrowAsync<InvalidOperationException>(
            () => target.Actor.ProcessTrustedEffectAsync(submission, context, "synthetic-proof"));
        string collisionKey = targetStore.CommittedState.Keys.Single(
            static key => key.StartsWith("effect_collision_", StringComparison.Ordinal));

        IActorProxyFactory proxies = Substitute.For<IActorProxyFactory>();
        _ = proxies.CreateActorProxy<IAggregateActor>(
            Arg.Is<ActorId>(id => id.GetId() == sourceIdentity.ActorId), nameof(AggregateActor))
            .Returns(source.Actor);
        _ = proxies.CreateActorProxy<IAggregateActor>(
            Arg.Is<ActorId>(id => id.GetId() == "test-tenant:test-domain:agg-001"), nameof(AggregateActor))
            .Returns(target.Actor);
        var retention = new ActorTrustedEffectJointRetentionPolicy(
            proxies, Options.Create(new EventStoreActorOptions()));
        const string keyActorId = "test-tenant:v1:key-a";
        var keyStore = new FaultInjectingActorStateManager();
        await keyStore.SeedCommittedStateAsync(new Dictionary<string, object>
        {
            [IdempotencyAdmissionActor.TombstoneStateName] = new IdempotencyAdmissionTombstone(
                IdempotencyAdmissionTombstone.CurrentSchemaVersion,
                IdempotencyAdmissionState.Expired, identity.Tenant, "key-a", "tag-a", "v1",
                Hexalith.EventStore.Contracts.Commands.IdempotencyReplayRetentionTier.Mutation,
                _now.AddDays(-2), _now.AddDays(-1), _now),
        });
        var keyActor = new IdempotencyAdmissionActor(
            ActorHost.CreateForTest<IdempotencyAdmissionActor>(
                new ActorTestOptions { ActorId = new ActorId(keyActorId) }),
            NullLogger<IdempotencyAdmissionActor>.Instance, new FakeTimeProvider(_now));
        ActorStateManagerTestHelper.SetStateManager(keyActor, keyStore);
        var directoryStore = new FaultInjectingActorStateManager();
        var alias = new IdempotencyAdmissionDirectoryAlias("v1", keyActorId, "key-a");
        string aliasKey = IdempotencyAdmissionDirectoryActor.BuildStateName(alias);
        await directoryStore.SeedCommittedStateAsync(new Dictionary<string, object> { [aliasKey] = "protected-alias" });
        var directory = new IdempotencyAdmissionDirectoryActor(
            ActorHost.CreateForTest<IdempotencyAdmissionDirectoryActor>(
                new ActorTestOptions { ActorId = new ActorId(identity.Tenant) }),
            NullLogger<IdempotencyAdmissionDirectoryActor>.Instance);
        ActorStateManagerTestHelper.SetStateManager(directory, directoryStore);
        var inventoryStore = new FaultInjectingActorStateManager();
        const string inventoryKey = "legacy:v1:key-a";
        await inventoryStore.SeedCommittedStateAsync(new Dictionary<string, object> { [inventoryKey] = "protected-inventory" });
        var inventory = new IdempotencyLegacyInventoryActor(
            ActorHost.CreateForTest<IdempotencyLegacyInventoryActor>(
                new ActorTestOptions { ActorId = new ActorId(identity.Tenant) }),
            Options.Create(new IdempotencyAdmissionOptions()));
        ActorStateManagerTestHelper.SetStateManager(inventory, inventoryStore);
        _ = proxies.CreateActorProxy<IIdempotencyAdmissionActor>(
            Arg.Is<ActorId>(id => id.GetId() == keyActorId), IdempotencyAdmissionActor.ActorTypeName)
            .Returns(keyActor);
        _ = proxies.CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(
            Arg.Is<ActorId>(id => id.GetId() == identity.Tenant), IdempotencyAdmissionDirectoryActor.ActorTypeName)
            .Returns(directory);
        _ = proxies.CreateActorProxy<IIdempotencyLegacyInventoryActor>(
            Arg.Is<ActorId>(id => id.GetId() == identity.Tenant), IdempotencyLegacyInventoryActor.ActorTypeName)
            .Returns(inventory);
        ActorHost host = ActorHost.CreateForTest<IdempotencyTenantLifecycleActor>(
            new ActorTestOptions { ActorId = new ActorId(identity.Tenant) });
        var lifecycleStore = new FaultInjectingActorStateManager();
        var lifecycle = new IdempotencyTenantLifecycleActor(
            host,
            NullLogger<IdempotencyTenantLifecycleActor>.Instance,
            new FakeTimeProvider(_now),
            actorProxyFactory: proxies,
            trustedEffectRetention: retention,
            trustedEffectAuditSink: Substitute.For<ITrustedEffectAuditSink>(),
            trustedEffectErasureAuthority: erasureAuthority);
        ActorStateManagerTestHelper.SetStateManager(lifecycle, lifecycleStore);

        await lifecycle.RegisterTrustedEffectAsync(identity);
        await lifecycle.RegisterAsync([new IdempotencyTenantLifecycleReference(keyActorId, "v1", "key-a")]);
        _ = await lifecycle.EnterDeletionAsync(_now.AddDays(-401));
        _ = await lifecycle.PlaceLegalHoldAsync(_now);
        await Should.ThrowAsync<InvalidOperationException>(() => lifecycle.PurgeAsync(1));
        sourceStore.CommittedState.Keys.ShouldContain(sourceEventKey);
        targetStore.CommittedState.Keys.ShouldContain(receiptKey);
        targetStore.CommittedState.Keys.ShouldContain(collisionKey);

        _ = await lifecycle.ReleaseLegalHoldAsync(_now);
        targetStore.FaultOnCall("TryRemoveState:" + receiptKey, 1, new IOException("target erasure interrupted"));
        await Should.ThrowAsync<IOException>(() => lifecycle.PurgeAsync(1));
        (await lifecycle.GetAsync()).State.ShouldBe(IdempotencyTenantLifecycleState.PurgeEligible);
        sourceStore.CommittedState.Keys.ShouldContain(sourceEventKey);
        targetStore.CommittedState.Keys.ShouldContain(receiptKey);
        targetStore.CommittedState.Keys.ShouldContain(collisionKey);

        var interrupted = (TrustedEffectErasureProgress)
            targetStore.CreateCommittedView()[TrustedEffectErasureProgress.StateName];
        TrustedEffectAggregateErasure stale = TrustedEffectErasureInventory.Build(
                identity.Tenant, [identity], _now.AddDays(-401), _now)
            .Single(request => request.Aggregate == identity.TargetAggregate)
            with { Nonce = interrupted.CapabilityNonce };
        stale = stale with { Capability = await erasureAuthority.IssueAsync(stale) };
        _ = await lifecycle.PlaceLegalHoldAsync(_now);
        await Should.ThrowAsync<InvalidOperationException>(() => target.Actor.EraseTrustedEffectEvidenceAsync(stale));
        await Should.ThrowAsync<InvalidOperationException>(() => lifecycle.PurgeAsync(1));
        targetStore.CreateCommittedView().Keys.ShouldContain(receiptKey);
        targetStore.CreateCommittedView().Keys.ShouldContain(collisionKey);
        _ = await lifecycle.ReleaseLegalHoldAsync(_now);

        IdempotencyTenantLifecycleRecord purged = await lifecycle.PurgeAsync(1);
        purged.State.ShouldBe(IdempotencyTenantLifecycleState.Purged);
        purged.TrustedEffectEvidenceErased.ShouldBeTrue();
        sourceStore.CommittedState.Keys.ShouldNotContain(sourceEventKey);
        sourceStore.CommittedState.Keys.ShouldNotContain(sourceIdentity.MetadataKey);
        targetStore.CommittedState.Keys.ShouldNotContain(receiptKey);
        targetStore.CommittedState.Keys.ShouldNotContain(collisionKey);
        targetStore.CommittedState.Keys.ShouldNotContain(key => key.Contains(":events:1", StringComparison.Ordinal));
        targetStore.CommittedState.Keys.ShouldContain(TrustedEffectErasureProgress.StateName);
        keyStore.CreateCommittedView().ShouldBeEmpty();
        directoryStore.CreateCommittedView().ShouldBeEmpty();
        inventoryStore.CreateCommittedView().ShouldBeEmpty();
        var persistedLifecycle = (IdempotencyTenantLifecycleRecord)
            lifecycleStore.CreateCommittedView()[IdempotencyTenantLifecycleActor.StateName];
        persistedLifecycle.State.ShouldBe(IdempotencyTenantLifecycleState.Purged);
        persistedLifecycle.TrustedEffectEvidenceErased.ShouldBeTrue();
        persistedLifecycle.TrustedEffects.ShouldBeEmpty();
        persistedLifecycle.PendingTrustedEffectIds.ShouldBeEmpty();
        persistedLifecycle.References.ShouldBeEmpty();
    }

    /// <summary>Deletion after registration but before the target turn fences the queued submission.</summary>
    [Fact]
    public async Task DeletionBetweenRegistrationAndTargetTurnCannotRecreateErasedOutcome()
    {
        var identity = new EffectIdentity(
            "test-tenant", "test-domain", "source-001", 1,
            EffectKindCatalog.DateResume, "test-domain", "agg-001", 0);
        var sourceIdentity = new AggregateIdentity(identity.Tenant, identity.SourceDomain, identity.SourceAggregate);
        var sourceStore = new FaultInjectingActorStateManager();
        await sourceStore.SeedCommittedStateAsync(new Dictionary<string, object>
        {
            [sourceIdentity.MetadataKey] = new AggregateMetadata(1, _now, null),
            [sourceIdentity.EventStreamKeyPrefix + "1"] = new Hexalith.EventStore.Server.Events.EventEnvelope(
                "source-event", identity.SourceAggregate, "work-item", identity.Tenant,
                identity.SourceDomain, 1, 1, _now, "source-correlation", "source-cause",
                "synthetic-test", "v1", "SourceOccurred", 1, "json", [], null),
        });
        var targetStore = new FaultInjectingActorStateManager();
        string messageId = EffectIdentityCodec.ComputeMessageId(identity);
        var submission = new TrustedEffectSubmission(identity, "CreateOrder", [1], messageId, messageId);
        var context = new TrustedEffectContext("reactor", "date-resume", "source-cause", "synthetic-token");
        ITrustedEffectAdmissionPolicy admissionPolicy = Substitute.For<ITrustedEffectAdmissionPolicy>();
        using var keys = new StaticIdempotencyDigestKeyProvider(
            "test-v1", new Dictionary<string, byte[]>
            {
                ["test-v1"] = Enumerable.Repeat((byte)7, 32).ToArray(),
            }, []);
        ITrustedEffectErasureAuthority erasureAuthority = new TrustedEffectErasureCapability(
            keys, new FakeTimeProvider(_now));
        _ = admissionPolicy.PrepareAsync(submission, context, Arg.Any<CancellationToken>())
            .Returns(new TrustedEffectAdmission(submission, context, "SYNTHETIC-DIGEST"));
        ActorTestContext source = AggregateActorTestHelper.CreateActor(
            stateManager: sourceStore, trustedEffectErasureAuthority: erasureAuthority,
            actorId: sourceIdentity.ActorId);
        ActorTestContext target = AggregateActorTestHelper.CreateActor(
            stateManager: targetStore, trustedEffectAdmissionPolicy: admissionPolicy,
            trustedEffectErasureAuthority: erasureAuthority);
        IActorProxyFactory proxies = Substitute.For<IActorProxyFactory>();
        _ = proxies.CreateActorProxy<IAggregateActor>(
            Arg.Is<ActorId>(id => id.GetId() == sourceIdentity.ActorId), nameof(AggregateActor))
            .Returns(source.Actor);
        _ = proxies.CreateActorProxy<IAggregateActor>(
            Arg.Is<ActorId>(id => id.GetId() == "test-tenant:test-domain:agg-001"), nameof(AggregateActor))
            .Returns(target.Actor);
        var retention = new ActorTrustedEffectJointRetentionPolicy(
            proxies, Options.Create(new EventStoreActorOptions()));
        var lifecycleStore = new FaultInjectingActorStateManager();
        ActorHost host = ActorHost.CreateForTest<IdempotencyTenantLifecycleActor>(
            new ActorTestOptions { ActorId = new ActorId(identity.Tenant) });
        var lifecycle = new IdempotencyTenantLifecycleActor(
            host, NullLogger<IdempotencyTenantLifecycleActor>.Instance,
            new FakeTimeProvider(_now), trustedEffectRetention: retention,
            trustedEffectAuditSink: Substitute.For<ITrustedEffectAuditSink>(),
            trustedEffectErasureAuthority: erasureAuthority);
        ActorStateManagerTestHelper.SetStateManager(lifecycle, lifecycleStore);

        await lifecycle.RegisterTrustedEffectAsync(identity);
        IdempotencyTenantLifecycleRecord registered = (IdempotencyTenantLifecycleRecord)
            lifecycleStore.CreateCommittedView()[IdempotencyTenantLifecycleActor.StateName];
        registered.PendingTrustedEffectIds.ShouldContain(EffectIdentityCodec.ComputeEffectId(identity));
        _ = await lifecycle.EnterDeletionAsync(_now.AddDays(-401));
        IdempotencyTenantLifecycleRecord purged = await lifecycle.PurgeAsync(1);
        purged.State.ShouldBe(IdempotencyTenantLifecycleState.Purged);

        await Should.ThrowAsync<InvalidOperationException>(() => target.Actor.ProcessTrustedEffectAsync(
            submission, context, "synthetic-proof"));
        _ = await target.Invoker.DidNotReceiveWithAnyArgs().InvokeAsync(default!, default, default);
        await admissionPolicy.DidNotReceiveWithAnyArgs().CompleteAsync(default!, default);
        sourceStore.CreateCommittedView().Keys.ShouldNotContain(sourceIdentity.MetadataKey);
        sourceStore.CreateCommittedView().Keys.ShouldNotContain(sourceIdentity.EventStreamKeyPrefix + "1");
        targetStore.CreateCommittedView().Keys.ShouldNotContain("effect_receipt_" + EffectIdentityCodec.ComputeEffectId(identity));
        targetStore.CreateCommittedView().Keys.ShouldContain(TrustedEffectErasureProgress.StateName);
    }

    /// <summary>Direct actor erasure cannot read or mutate state without a lifecycle proof verifier.</summary>
    [Fact]
    public async Task DirectErasureWithoutLifecycleAuthorityFailsClosed()
    {
        var state = new FaultInjectingActorStateManager();
        ActorTestContext target = AggregateActorTestHelper.CreateActor(stateManager: state);

        await Should.ThrowAsync<InvalidOperationException>(() => target.Actor.EraseTrustedEffectEvidenceAsync(
            new TrustedEffectAggregateErasure(
                "test-tenant", "test-domain", "agg-001", [], "digest",
                _now.AddDays(-401), _now, _now.AddMinutes(5),
                "00000000000000000000000000000000", string.Empty)));

        state.Trace.ShouldBeEmpty();
        state.CreateCommittedView().ShouldBeEmpty();
    }

    /// <summary>A signed decision cannot be forged or moved to a different partition or inventory.</summary>
    [Fact]
    public async Task ForgedOrChangedErasureCapabilityCannotReadActorState()
    {
        using var keys = new StaticIdempotencyDigestKeyProvider(
            "test-v1", new Dictionary<string, byte[]>
            {
                ["test-v1"] = Enumerable.Repeat((byte)7, 32).ToArray(),
            }, []);
        ITrustedEffectErasureAuthority authority = new TrustedEffectErasureCapability(
            keys, new FakeTimeProvider(_now));
        var identity = new EffectIdentity(
            "test-tenant", "test-domain", "source-001", 1,
            EffectKindCatalog.DateResume, "test-domain", "agg-001", 0);
        TrustedEffectAggregateErasure unsigned = TrustedEffectErasureInventory.Build(
                identity.Tenant, [identity], _now.AddDays(-401), _now)
            .Single(request => request.Aggregate == identity.TargetAggregate);
        var signed = unsigned with { Capability = await authority.IssueAsync(unsigned) };
        await authority.ValidateAsync(signed);
        var state = new FaultInjectingActorStateManager();
        ActorTestContext target = AggregateActorTestHelper.CreateActor(
            stateManager: state, trustedEffectErasureAuthority: authority);

        await Should.ThrowAsync<InvalidOperationException>(() => target.Actor.EraseTrustedEffectEvidenceAsync(
            signed with { Capability = "test-v1." + new string('0', 64) }));
        await Should.ThrowAsync<InvalidOperationException>(() => target.Actor.EraseTrustedEffectEvidenceAsync(
            signed with { InventoryDigest = "CHANGED" }));
        await Should.ThrowAsync<InvalidOperationException>(() => target.Actor.EraseTrustedEffectEvidenceAsync(
            signed with { EffectIds = [] }));
        await Should.ThrowAsync<InvalidOperationException>(() => target.Actor.EraseTrustedEffectEvidenceAsync(
            signed with { DeletionApprovedAt = signed.DeletionApprovedAt.AddTicks(-1) }));
        await Should.ThrowAsync<InvalidOperationException>(() => target.Actor.EraseTrustedEffectEvidenceAsync(
            signed with { Aggregate = "other-target" }));

        state.Trace.ShouldBeEmpty();
        state.CreateCommittedView().ShouldBeEmpty();
    }

    /// <summary>A purge waiting for the target turn can finish before gateway completion calls lifecycle.</summary>
    [Fact]
    public async Task PurgeOverlappingTargetTurnDoesNotDeadlockLifecycleCompletion()
    {
        var identity = new EffectIdentity(
            "test-tenant", "test-domain", "agg-001", 1,
            EffectKindCatalog.DateResume, "test-domain", "agg-001", 0);
        string messageId = EffectIdentityCodec.ComputeMessageId(identity);
        var submission = new TrustedEffectSubmission(identity, "CreateOrder", [1], messageId, messageId);
        var context = new TrustedEffectContext("reactor", "date-resume", "source-cause", "synthetic-token");
        var targetEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var erasureEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var targetTurn = new TaskCompletionSource<TrustedEffectResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        IAggregateActor target = Substitute.For<IAggregateActor>();
        _ = target.ProcessTrustedEffectAsync(submission, context, "synthetic-proof").Returns(_ =>
        {
            targetEntered.SetResult();
            return targetTurn.Task;
        });
        _ = target.EraseTrustedEffectEvidenceAsync(Arg.Any<TrustedEffectAggregateErasure>()).Returns(async _ =>
        {
            erasureEntered.SetResult();
            await targetTurn.Task.ConfigureAwait(false);
        });

        IActorProxyFactory proxies = Substitute.For<IActorProxyFactory>();
        _ = proxies.CreateActorProxy<IAggregateActor>(
            Arg.Is<ActorId>(id => id.GetId() == "test-tenant:test-domain:agg-001"), nameof(AggregateActor))
            .Returns(target);
        var retention = new ActorTrustedEffectJointRetentionPolicy(
            proxies, Options.Create(new EventStoreActorOptions()));
        using var keys = new StaticIdempotencyDigestKeyProvider(
            "test-v1", new Dictionary<string, byte[]>
            {
                ["test-v1"] = Enumerable.Repeat((byte)7, 32).ToArray(),
            }, []);
        var lifecycle = new IdempotencyTenantLifecycleActor(
            ActorHost.CreateForTest<IdempotencyTenantLifecycleActor>(
                new ActorTestOptions { ActorId = new ActorId(identity.Tenant) }),
            NullLogger<IdempotencyTenantLifecycleActor>.Instance,
            new FakeTimeProvider(_now),
            trustedEffectRetention: retention,
            trustedEffectAuditSink: Substitute.For<ITrustedEffectAuditSink>(),
            trustedEffectErasureAuthority: new TrustedEffectErasureCapability(
                keys, new FakeTimeProvider(_now)));
        ActorStateManagerTestHelper.SetStateManager(lifecycle, new FaultInjectingActorStateManager());
        await lifecycle.RegisterTrustedEffectAsync(identity);

        Task<IdempotencyTenantLifecycleRecord>? purge = null;
        ITrustedEffectRetentionGate gate = Substitute.For<ITrustedEffectRetentionGate>();
        _ = gate.CompleteAsync(identity, Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            // Model the serialized lifecycle turn while purge awaits target erasure.
            await purge!.ConfigureAwait(false);
            await lifecycle.CompleteTrustedEffectAsync(identity).ConfigureAwait(false);
        });
        var router = new TrustedEffectRouter(
            proxies, Options.Create(new EventStoreActorOptions()), gate);
        Task<TrustedEffectResult> routed = router.RouteAsync(submission, context, "synthetic-proof");
        await targetEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _ = await lifecycle.EnterDeletionAsync(_now.AddDays(-401));
        purge = lifecycle.PurgeAsync(1);
        await erasureEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await gate.DidNotReceiveWithAnyArgs().CompleteAsync(default!);

        targetTurn.SetResult(new TrustedEffectResult(
            EffectIdentityCodec.ComputeEffectId(identity), TrustedEffectDisposition.Success, false, null));
        (await purge.WaitAsync(TimeSpan.FromSeconds(5))).State.ShouldBe(IdempotencyTenantLifecycleState.Purged);
        await Should.ThrowAsync<InvalidOperationException>(
            async () => _ = await routed.WaitAsync(TimeSpan.FromSeconds(5)));
        await gate.Received(1).CompleteAsync(identity, Arg.Any<CancellationToken>());
    }
}
