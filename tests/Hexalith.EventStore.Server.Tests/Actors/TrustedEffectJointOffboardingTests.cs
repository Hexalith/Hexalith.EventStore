using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

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
            [sourceEventKey] = new Hexalith.EventStore.Server.Events.EventEnvelope(
                "source-event", identity.SourceAggregate, "work-item", identity.Tenant,
                identity.SourceDomain, identity.SourceEnvelopeSequence, identity.SourceEnvelopeSequence,
                _now, "source-correlation", "source-cause", "synthetic-test", "v1",
                "SourceOccurred", 1, "json", [], null),
        });

        ITrustedEffectAdmissionPolicy admissionPolicy = Substitute.For<ITrustedEffectAdmissionPolicy>();
        _ = admissionPolicy.AdmitAsync(submission, context, Arg.Any<CancellationToken>()).Returns(admission);
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        _ = invoker.InvokeAsync(
                Arg.Any<Hexalith.EventStore.Contracts.Commands.CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new AggregateActorTestHelper.TestEvent()]));
        ActorTestContext target = AggregateActorTestHelper.CreateActor(
            stateManager: targetStore,
            invoker: invoker,
            trustedEffectAdmissionPolicy: admissionPolicy);
        TrustedEffectResult outcome = await target.Actor.ProcessTrustedEffectAsync(
            submission, context, "synthetic-proof");
        outcome.Disposition.ShouldBe(TrustedEffectDisposition.Success);
        string receiptKey = "effect_receipt_" + outcome.EffectId;
        sourceStore.CommittedState.Keys.ShouldContain(sourceEventKey);
        targetStore.CommittedState.Keys.ShouldContain(receiptKey);
        targetStore.CommittedState.Keys.ShouldContain(key => key.Contains(":events:1", StringComparison.Ordinal));
        _ = admissionPolicy.AdmitAsync(submission, context, Arg.Any<CancellationToken>())
            .Returns(admission with { SemanticDigest = "COLLIDING-SYNTHETIC-DIGEST" });
        await Should.ThrowAsync<InvalidOperationException>(
            () => target.Actor.ProcessTrustedEffectAsync(submission, context, "synthetic-proof"));
        string collisionKey = targetStore.CommittedState.Keys.Single(
            static key => key.StartsWith("effect_collision_", StringComparison.Ordinal));

        var retention = new SyntheticTrustedEffectJointRetentionPolicy(
            identity.Tenant, sourceStore, targetStore);
        ActorHost host = ActorHost.CreateForTest<IdempotencyTenantLifecycleActor>(
            new ActorTestOptions { ActorId = new ActorId(identity.Tenant) });
        var lifecycleStore = new FaultInjectingActorStateManager();
        var lifecycle = new IdempotencyTenantLifecycleActor(
            host,
            NullLogger<IdempotencyTenantLifecycleActor>.Instance,
            new FakeTimeProvider(_now),
            trustedEffectRetention: retention,
            trustedEffectAuditSink: Substitute.For<ITrustedEffectAuditSink>());
        ActorStateManagerTestHelper.SetStateManager(lifecycle, lifecycleStore);

        await lifecycle.RegisterTrustedEffectAsync(identity);
        _ = await lifecycle.EnterDeletionAsync(_now.AddDays(-401));
        _ = await lifecycle.PlaceLegalHoldAsync(_now);
        await Should.ThrowAsync<InvalidOperationException>(() => lifecycle.PurgeAsync(1));
        sourceStore.CommittedState.Keys.ShouldContain(sourceEventKey);
        targetStore.CommittedState.Keys.ShouldContain(receiptKey);
        targetStore.CommittedState.Keys.ShouldContain(collisionKey);

        _ = await lifecycle.ReleaseLegalHoldAsync(_now);
        targetStore.FaultOnCall("RemoveState:" + receiptKey, 1, new IOException("target erasure interrupted"));
        await Should.ThrowAsync<IOException>(() => lifecycle.PurgeAsync(1));
        (await lifecycle.GetAsync()).State.ShouldBe(IdempotencyTenantLifecycleState.PurgeEligible);
        sourceStore.CommittedState.ShouldBeEmpty();
        targetStore.CommittedState.Keys.ShouldContain(receiptKey);
        targetStore.CommittedState.Keys.ShouldContain(collisionKey);

        IdempotencyTenantLifecycleRecord purged = await lifecycle.PurgeAsync(1);
        purged.State.ShouldBe(IdempotencyTenantLifecycleState.Purged);
        purged.TrustedEffectEvidenceErased.ShouldBeTrue();
        sourceStore.CommittedState.ShouldBeEmpty();
        targetStore.CommittedState.ShouldBeEmpty();
        var persistedLifecycle = (IdempotencyTenantLifecycleRecord)
            lifecycleStore.CreateCommittedView()[IdempotencyTenantLifecycleActor.StateName];
        persistedLifecycle.State.ShouldBe(IdempotencyTenantLifecycleState.Purged);
        persistedLifecycle.TrustedEffectEvidenceErased.ShouldBeTrue();
    }
}
