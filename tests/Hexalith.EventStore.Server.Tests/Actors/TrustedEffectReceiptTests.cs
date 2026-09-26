using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.DomainServices;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>Target actor receipt behavior under a committed no-op outcome.</summary>
public class TrustedEffectReceiptTests
{
    /// <summary>Exact replay reads the committed receipt without invoking the domain again.</summary>
    [Fact]
    public async Task ExactReplayReturnsReceiptWithoutHandle()
    {
        var state = new FaultInjectingActorStateManager();
        ITrustedEffectAdmissionPolicy policy = Substitute.For<ITrustedEffectAdmissionPolicy>();
        (TrustedEffectSubmission submission, TrustedEffectContext context, TrustedEffectAdmission admission) = CreateEffect();
        _ = policy.AdmitAsync(submission, context, Arg.Any<CancellationToken>()).Returns(admission);
        ActorTestContext actor = AggregateActorTestHelper.CreateActor(
            stateManager: state,
            trustedEffectAdmissionPolicy: policy);

        TrustedEffectResult first = await actor.Actor.ProcessTrustedEffectAsync(submission, context, "test-proof");
        TrustedEffectResult replay = await actor.Actor.ProcessTrustedEffectAsync(submission, context, "test-proof");

        first.Disposition.ShouldBe(TrustedEffectDisposition.NoOp);
        first.Replayed.ShouldBeFalse();
        replay.Disposition.ShouldBe(TrustedEffectDisposition.NoOp);
        replay.Replayed.ShouldBeTrue();
        _ = await actor.Invoker.Received(1).InvokeAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Commands.CommandEnvelope>(),
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());
        state.CommittedState.Keys.ShouldContain("effect_receipt_" + first.EffectId);
    }

    /// <summary>A changed semantic command with the same identity cannot invoke Handle again.</summary>
    [Fact]
    public async Task SemanticCollisionDoesNotHandleAgain()
    {
        var state = new FaultInjectingActorStateManager();
        ITrustedEffectAdmissionPolicy policy = Substitute.For<ITrustedEffectAdmissionPolicy>();
        (TrustedEffectSubmission submission, TrustedEffectContext context, TrustedEffectAdmission admission) = CreateEffect();
        _ = policy.AdmitAsync(submission, context, Arg.Any<CancellationToken>()).Returns(admission);
        ActorTestContext actor = AggregateActorTestHelper.CreateActor(
            stateManager: state,
            trustedEffectAdmissionPolicy: policy);
        _ = await actor.Actor.ProcessTrustedEffectAsync(submission, context, "test-proof");
        _ = policy.AdmitAsync(submission, context, Arg.Any<CancellationToken>())
            .Returns(admission with { SemanticDigest = "CHANGED" });

        await Should.ThrowAsync<InvalidOperationException>(
            () => actor.Actor.ProcessTrustedEffectAsync(submission, context, "test-proof"));
        _ = await actor.Invoker.Received(1).InvokeAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Commands.CommandEnvelope>(),
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());
        state.CommittedState.Keys.ShouldContain(key => key.StartsWith("effect_collision_", StringComparison.Ordinal));
    }

    /// <summary>An unavailable privileged audit sink prevents a collision quarantine write.</summary>
    [Fact]
    public async Task CollisionAuditFailureDoesNotMutateTarget()
    {
        var state = new FaultInjectingActorStateManager();
        ITrustedEffectAdmissionPolicy policy = Substitute.For<ITrustedEffectAdmissionPolicy>();
        ITrustedEffectAuditSink audit = Substitute.For<ITrustedEffectAuditSink>();
        (TrustedEffectSubmission submission, TrustedEffectContext context, TrustedEffectAdmission admission) = CreateEffect();
        _ = policy.AdmitAsync(submission, context, Arg.Any<CancellationToken>()).Returns(admission);
        ActorTestContext actor = AggregateActorTestHelper.CreateActor(
            stateManager: state,
            trustedEffectAdmissionPolicy: policy,
            trustedEffectAuditSink: audit);
        _ = await actor.Actor.ProcessTrustedEffectAsync(submission, context, "test-proof");
        _ = policy.AdmitAsync(submission, context, Arg.Any<CancellationToken>())
            .Returns(admission with { SemanticDigest = "CHANGED" });
        _ = audit.AppendAsync(Arg.Any<TrustedEffectAuditRecord>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new IOException("audit unavailable"));

        await Should.ThrowAsync<IOException>(
            () => actor.Actor.ProcessTrustedEffectAsync(submission, context, "test-proof"));

        state.CommittedState.Keys.ShouldNotContain(key => key.StartsWith("effect_collision_", StringComparison.Ordinal));
        _ = await actor.Invoker.Received(1).InvokeAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Commands.CommandEnvelope>(),
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Admission must complete before target receipt state is read.</summary>
    [Fact]
    public async Task DeniedCallerCannotInspectReceipt()
    {
        var state = new FaultInjectingActorStateManager();
        ITrustedEffectAdmissionPolicy policy = Substitute.For<ITrustedEffectAdmissionPolicy>();
        (TrustedEffectSubmission submission, TrustedEffectContext context, _) = CreateEffect();
        _ = policy.AdmitAsync(submission, context, Arg.Any<CancellationToken>())
            .Returns<TrustedEffectAdmission>(_ => throw new InvalidOperationException("Denied"));
        ActorTestContext actor = AggregateActorTestHelper.CreateActor(
            stateManager: state,
            trustedEffectAdmissionPolicy: policy);

        await Should.ThrowAsync<InvalidOperationException>(
            () => actor.Actor.ProcessTrustedEffectAsync(submission, context, "test-proof"));
        state.Trace.ShouldBeEmpty();
    }

    /// <summary>A failed receipt write prevents a no-op from being reported as accepted.</summary>
    [Fact]
    public async Task ReceiptWriteFailureDoesNotReturnSuccess()
    {
        var state = new FaultInjectingActorStateManager();
        ITrustedEffectAdmissionPolicy policy = Substitute.For<ITrustedEffectAdmissionPolicy>();
        (TrustedEffectSubmission submission, TrustedEffectContext context, TrustedEffectAdmission admission) = CreateEffect();
        _ = policy.AdmitAsync(submission, context, Arg.Any<CancellationToken>()).Returns(admission);
        ActorTestContext actor = AggregateActorTestHelper.CreateActor(
            stateManager: state,
            trustedEffectAdmissionPolicy: policy);
        string receiptKey = "effect_receipt_" + EffectIdentityCodec.ComputeEffectId(submission.Identity);
        state.FaultOnCall("SetState:" + receiptKey, 1, new IOException("receipt write failed"));

        await Should.ThrowAsync<Exception>(() => actor.Actor.ProcessTrustedEffectAsync(submission, context, "test-proof"));
        state.CommittedState.ContainsKey(receiptKey).ShouldBeFalse();
    }

    /// <summary>The first target event and receipt appear in one committed state snapshot.</summary>
    [Fact]
    public async Task EventfulOutcomeCommitsReceiptWithEventBatch()
    {
        var state = new FaultInjectingActorStateManager();
        ITrustedEffectAdmissionPolicy policy = Substitute.For<ITrustedEffectAdmissionPolicy>();
        (TrustedEffectSubmission submission, TrustedEffectContext context, TrustedEffectAdmission admission) = CreateEffect();
        _ = policy.AdmitAsync(submission, context, Arg.Any<CancellationToken>()).Returns(admission);
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        _ = invoker.InvokeAsync(
                Arg.Any<Hexalith.EventStore.Contracts.Commands.CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new AggregateActorTestHelper.TestEvent()]));
        ActorTestContext actor = AggregateActorTestHelper.CreateActor(
            stateManager: state,
            invoker: invoker,
            trustedEffectAdmissionPolicy: policy);

        TrustedEffectResult result = await actor.Actor.ProcessTrustedEffectAsync(submission, context, "test-proof");

        result.Disposition.ShouldBe(TrustedEffectDisposition.Success);
        string receiptKey = "effect_receipt_" + result.EffectId;
        state.CommittedSnapshots.ShouldContain(snapshot =>
            snapshot.ContainsKey(receiptKey)
            && snapshot.Keys.Any(key => key.Contains(":events:1", StringComparison.Ordinal)));
    }

    /// <summary>A lost acknowledgement after the event batch can be recovered from its receipt.</summary>
    [Fact]
    public async Task EventBatchAcknowledgementLossPreservesReceipt()
    {
        var state = new FaultInjectingActorStateManager();
        ITrustedEffectAdmissionPolicy policy = Substitute.For<ITrustedEffectAdmissionPolicy>();
        (TrustedEffectSubmission submission, TrustedEffectContext context, TrustedEffectAdmission admission) = CreateEffect();
        _ = policy.AdmitAsync(submission, context, Arg.Any<CancellationToken>()).Returns(admission);
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        _ = invoker.InvokeAsync(
                Arg.Any<Hexalith.EventStore.Contracts.Commands.CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new AggregateActorTestHelper.TestEvent()]));
        ActorTestContext actor = AggregateActorTestHelper.CreateActor(
            stateManager: state,
            invoker: invoker,
            trustedEffectAdmissionPolicy: policy);
        state.FaultAfterCall("SaveState", 3, new IOException("acknowledgement lost"));

        TrustedEffectResult first = await actor.Actor.ProcessTrustedEffectAsync(submission, context, "test-proof");
        first.Disposition.ShouldBe(TrustedEffectDisposition.Success);
        string receiptKey = "effect_receipt_" + EffectIdentityCodec.ComputeEffectId(submission.Identity);
        state.CommittedState.ContainsKey(receiptKey).ShouldBeTrue();
        TrustedEffectResult replay = await actor.Actor.ProcessTrustedEffectAsync(submission, context, "test-proof");
        replay.Replayed.ShouldBeTrue();
        _ = await invoker.Received(1).InvokeAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Commands.CommandEnvelope>(),
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());
    }

    private static (TrustedEffectSubmission, TrustedEffectContext, TrustedEffectAdmission) CreateEffect()
    {
        var identity = new EffectIdentity(
            "test-tenant", "test-domain", "source-001", 1,
            EffectKindCatalog.DateResume, "test-domain", "agg-001", 0);
        string messageId = EffectIdentityCodec.ComputeMessageId(identity);
        var submission = new TrustedEffectSubmission(identity, "CreateOrder", [1, 2, 3], messageId, messageId);
        var context = new TrustedEffectContext("reactor", "date-resume", "source-cause", "signed-token");
        return (submission, context, new TrustedEffectAdmission(submission, context, "DIGEST"));
    }
}
