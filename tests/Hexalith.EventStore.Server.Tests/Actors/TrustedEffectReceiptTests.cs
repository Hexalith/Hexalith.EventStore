using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
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
        _ = policy.PrepareAsync(submission, context, Arg.Any<CancellationToken>()).Returns(admission);
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
        _ = policy.DidNotReceiveWithAnyArgs().CompleteAsync(default!);
        state.CommittedState.Keys.ShouldContain("effect_receipt_" + first.EffectId);
    }

    /// <summary>A changed semantic command with the same identity cannot invoke Handle again.</summary>
    [Fact]
    public async Task SemanticCollisionDoesNotHandleAgain()
    {
        var state = new FaultInjectingActorStateManager();
        ITrustedEffectAdmissionPolicy policy = Substitute.For<ITrustedEffectAdmissionPolicy>();
        (TrustedEffectSubmission submission, TrustedEffectContext context, TrustedEffectAdmission admission) = CreateEffect();
        _ = policy.PrepareAsync(submission, context, Arg.Any<CancellationToken>()).Returns(admission);
        ActorTestContext actor = AggregateActorTestHelper.CreateActor(
            stateManager: state,
            trustedEffectAdmissionPolicy: policy);
        _ = await actor.Actor.ProcessTrustedEffectAsync(submission, context, "test-proof");
        _ = policy.PrepareAsync(submission, context, Arg.Any<CancellationToken>())
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
        _ = policy.PrepareAsync(submission, context, Arg.Any<CancellationToken>()).Returns(admission);
        ActorTestContext actor = AggregateActorTestHelper.CreateActor(
            stateManager: state,
            trustedEffectAdmissionPolicy: policy,
            trustedEffectAuditSink: audit);
        _ = await actor.Actor.ProcessTrustedEffectAsync(submission, context, "test-proof");
        _ = policy.PrepareAsync(submission, context, Arg.Any<CancellationToken>())
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
        _ = policy.PrepareAsync(submission, context, Arg.Any<CancellationToken>())
            .Returns<TrustedEffectAdmission>(_ => throw new InvalidOperationException("Denied"));
        ActorTestContext actor = AggregateActorTestHelper.CreateActor(
            stateManager: state,
            trustedEffectAdmissionPolicy: policy);

        await Should.ThrowAsync<InvalidOperationException>(
            () => actor.Actor.ProcessTrustedEffectAsync(submission, context, "test-proof"));
        state.Trace.ShouldBeEmpty();
    }

    /// <summary>A forged direct actor call cannot register evidence or inspect receipt state.</summary>
    [Fact]
    public async Task InvalidGatewayProofDeniesBeforeRetentionMutation()
    {
        var state = new FaultInjectingActorStateManager();
        ITrustedEffectAdmissionPolicy policy = Substitute.For<ITrustedEffectAdmissionPolicy>();
        ITrustedEffectGatewayProof proof = Substitute.For<ITrustedEffectGatewayProof>();
        ITrustedEffectAuditSink audit = Substitute.For<ITrustedEffectAuditSink>();
        (TrustedEffectSubmission submission, TrustedEffectContext context, TrustedEffectAdmission admission) = CreateEffect();
        _ = policy.PrepareAsync(submission, context, Arg.Any<CancellationToken>()).Returns(admission);
        _ = proof.ValidateAsync(admission, "forged", Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("Invalid proof"));
        ActorTestContext actor = AggregateActorTestHelper.CreateActor(
            stateManager: state,
            trustedEffectAdmissionPolicy: policy,
            trustedEffectAuditSink: audit,
            trustedEffectGatewayProof: proof);

        await Should.ThrowAsync<InvalidOperationException>(
            () => actor.Actor.ProcessTrustedEffectAsync(submission, context, "forged"));

        _ = policy.DidNotReceiveWithAnyArgs().CompleteAsync(default!);
        state.Trace.ShouldBeEmpty();
        await audit.Received(1).AppendAsync(
            Arg.Is<TrustedEffectAuditRecord>(record => record.Action == "gateway-proof"
                && record.Disposition == "denied"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>A proof for another actor partition cannot register tenant evidence here.</summary>
    [Fact]
    public async Task WrongTargetActorDeniesBeforeRetentionMutation()
    {
        var state = new FaultInjectingActorStateManager();
        ITrustedEffectAdmissionPolicy policy = Substitute.For<ITrustedEffectAdmissionPolicy>();
        ITrustedEffectAuditSink audit = Substitute.For<ITrustedEffectAuditSink>();
        (TrustedEffectSubmission submission, TrustedEffectContext context, _) = CreateEffect();
        EffectIdentity wrongTarget = submission.Identity with { TargetAggregate = "another-target" };
        string messageId = EffectIdentityCodec.ComputeMessageId(wrongTarget);
        submission = submission with
        {
            Identity = wrongTarget,
            MessageId = messageId,
            IdempotencyKey = messageId,
        };
        _ = policy.PrepareAsync(submission, context, Arg.Any<CancellationToken>())
            .Returns(new TrustedEffectAdmission(submission, context, "DIGEST"));
        ActorTestContext actor = AggregateActorTestHelper.CreateActor(
            stateManager: state,
            trustedEffectAdmissionPolicy: policy,
            trustedEffectAuditSink: audit);

        await Should.ThrowAsync<InvalidOperationException>(
            () => actor.Actor.ProcessTrustedEffectAsync(submission, context, "test-proof"));

        _ = policy.DidNotReceiveWithAnyArgs().CompleteAsync(default!);
        state.Trace.ShouldBeEmpty();
        await audit.Received(1).AppendAsync(
            Arg.Is<TrustedEffectAuditRecord>(record => record.Action == "target-partition"
                && record.Disposition == "denied"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>A failed receipt write prevents a no-op from being reported as accepted.</summary>
    [Fact]
    public async Task ReceiptWriteFailureDoesNotReturnSuccess()
    {
        var state = new FaultInjectingActorStateManager();
        ITrustedEffectAdmissionPolicy policy = Substitute.For<ITrustedEffectAdmissionPolicy>();
        (TrustedEffectSubmission submission, TrustedEffectContext context, TrustedEffectAdmission admission) = CreateEffect();
        _ = policy.PrepareAsync(submission, context, Arg.Any<CancellationToken>()).Returns(admission);
        ActorTestContext actor = AggregateActorTestHelper.CreateActor(
            stateManager: state,
            trustedEffectAdmissionPolicy: policy);
        string receiptKey = "effect_receipt_" + EffectIdentityCodec.ComputeEffectId(submission.Identity);
        state.FaultOnCall("SetState:" + receiptKey, 1, new IOException("receipt write failed"));

        await Should.ThrowAsync<IOException>(() => actor.Actor.ProcessTrustedEffectAsync(submission, context, "test-proof"));
        state.CommittedState.ContainsKey(receiptKey).ShouldBeFalse();
    }

    /// <summary>The first target event and receipt appear in one committed state snapshot.</summary>
    [Fact]
    public async Task EventfulOutcomeCommitsReceiptWithEventBatch()
    {
        var state = new FaultInjectingActorStateManager();
        ITrustedEffectAdmissionPolicy policy = Substitute.For<ITrustedEffectAdmissionPolicy>();
        (TrustedEffectSubmission submission, TrustedEffectContext context, TrustedEffectAdmission admission) = CreateEffect();
        _ = policy.PrepareAsync(submission, context, Arg.Any<CancellationToken>()).Returns(admission);
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
        _ = policy.PrepareAsync(submission, context, Arg.Any<CancellationToken>()).Returns(admission);
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
        var restoredState = new FaultInjectingActorStateManager();
        await restoredState.SeedCommittedStateAsync(state.CreateCommittedView());
        IDomainServiceInvoker restoredInvoker = Substitute.For<IDomainServiceInvoker>();
        ActorTestContext restored = AggregateActorTestHelper.CreateActor(
            stateManager: restoredState,
            invoker: restoredInvoker,
            trustedEffectAdmissionPolicy: policy);
        TrustedEffectResult replay = await restored.Actor.ProcessTrustedEffectAsync(
            submission, context, "test-proof");
        replay.Replayed.ShouldBeTrue();
        _ = await invoker.Received(1).InvokeAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Commands.CommandEnvelope>(),
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());
        _ = await restoredInvoker.DidNotReceiveWithAnyArgs().InvokeAsync(default!, default, default);
    }

    /// <summary>A domain rejection is a recorded target outcome whose receipt commits with the rejection event.</summary>
    [Fact]
    public async Task RejectionOutcomeCommitsRejectionReceiptWithEventBatch()
    {
        var state = new FaultInjectingActorStateManager();
        ITrustedEffectAdmissionPolicy policy = Substitute.For<ITrustedEffectAdmissionPolicy>();
        (TrustedEffectSubmission submission, TrustedEffectContext context, TrustedEffectAdmission admission) = CreateEffect();
        _ = policy.PrepareAsync(submission, context, Arg.Any<CancellationToken>()).Returns(admission);
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        _ = invoker.InvokeAsync(
                Arg.Any<Hexalith.EventStore.Contracts.Commands.CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(DomainResult.Rejection([new AggregateActorTestHelper.TestRejectionEvent()]));
        ActorTestContext actor = AggregateActorTestHelper.CreateActor(
            stateManager: state,
            invoker: invoker,
            trustedEffectAdmissionPolicy: policy);

        TrustedEffectResult first = await actor.Actor.ProcessTrustedEffectAsync(submission, context, "test-proof");
        TrustedEffectResult replay = await actor.Actor.ProcessTrustedEffectAsync(submission, context, "test-proof");

        first.Disposition.ShouldBe(TrustedEffectDisposition.Rejection);
        first.Replayed.ShouldBeFalse();
        replay.Disposition.ShouldBe(TrustedEffectDisposition.Rejection);
        replay.Replayed.ShouldBeTrue();
        string receiptKey = "effect_receipt_" + first.EffectId;
        state.CommittedSnapshots.ShouldContain(snapshot =>
            snapshot.ContainsKey(receiptKey)
            && snapshot.Keys.Any(key => key.Contains(":events:1", StringComparison.Ordinal)));
        var receipt = (EffectReceipt)state.CreateCommittedView()[receiptKey];
        receipt.Disposition.ShouldBe(TrustedEffectDisposition.Rejection);
        receipt.Identity.ShouldBe(submission.Identity);
        receipt.SemanticDigest.ShouldBe(admission.SemanticDigest);
        receipt.Workload.ShouldBe(context.Workload);
        receipt.Purpose.ShouldBe(context.Purpose);
        receipt.CausationId.ShouldBe(context.CausationId);
        state.CommittedState.Keys.ShouldNotContain(key => key.Contains(":events:2", StringComparison.Ordinal));
        _ = await invoker.Received(1).InvokeAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Commands.CommandEnvelope>(),
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>A colliding replay of an eventful outcome appends nothing and leaves audited quarantine evidence.</summary>
    [Fact]
    public async Task CollisionAfterEventfulOutcomeAppendsNoTargetEventAndAuditsQuarantine()
    {
        var state = new FaultInjectingActorStateManager();
        ITrustedEffectAdmissionPolicy policy = Substitute.For<ITrustedEffectAdmissionPolicy>();
        ITrustedEffectAuditSink audit = Substitute.For<ITrustedEffectAuditSink>();
        (TrustedEffectSubmission submission, TrustedEffectContext context, TrustedEffectAdmission admission) = CreateEffect();
        _ = policy.PrepareAsync(submission, context, Arg.Any<CancellationToken>()).Returns(admission);
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        _ = invoker.InvokeAsync(
                Arg.Any<Hexalith.EventStore.Contracts.Commands.CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new AggregateActorTestHelper.TestEvent()]));
        ActorTestContext actor = AggregateActorTestHelper.CreateActor(
            stateManager: state,
            invoker: invoker,
            trustedEffectAdmissionPolicy: policy,
            trustedEffectAuditSink: audit);
        TrustedEffectResult first = await actor.Actor.ProcessTrustedEffectAsync(submission, context, "test-proof");
        first.Disposition.ShouldBe(TrustedEffectDisposition.Success);
        _ = policy.PrepareAsync(submission, context, Arg.Any<CancellationToken>())
            .Returns(admission with { SemanticDigest = "CHANGED" });

        await Should.ThrowAsync<InvalidOperationException>(
            () => actor.Actor.ProcessTrustedEffectAsync(submission, context, "test-proof"));

        IReadOnlyDictionary<string, object> committed = state.CreateCommittedView();
        committed.Keys.ShouldContain(key => key.Contains(":events:1", StringComparison.Ordinal));
        committed.Keys.ShouldNotContain(key => key.Contains(":events:2", StringComparison.Ordinal));
        ((EffectReceipt)committed["effect_receipt_" + first.EffectId]).SemanticDigest.ShouldBe("DIGEST");
        var collision = (EffectCollisionRecord)committed.Single(
            static entry => entry.Key.StartsWith("effect_collision_", StringComparison.Ordinal)).Value;
        collision.EffectId.ShouldBe(first.EffectId);
        collision.OriginalSemanticDigest.ShouldBe("DIGEST");
        collision.AttemptedSemanticDigest.ShouldBe("CHANGED");
        collision.AttemptedIdentity.ShouldBe(submission.Identity);
        await audit.Received(1).AppendAsync(
            Arg.Is<TrustedEffectAuditRecord>(record => record.Action == "collision"
                && record.Disposition == "quarantined"
                && record.EffectId == first.EffectId
                && record.Tenant == submission.Identity.Tenant),
            Arg.Any<CancellationToken>());
        _ = await invoker.Received(1).InvokeAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Commands.CommandEnvelope>(),
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Expired idempotency and gateway status records do not authorize redispatch; the committed
    /// target receipt alone decides replay after a restore.
    /// </summary>
    [Fact]
    public async Task ExpiredIdempotencyAndStatusRecordsDoNotAuthorizeRedispatch()
    {
        var state = new FaultInjectingActorStateManager();
        ITrustedEffectAdmissionPolicy policy = Substitute.For<ITrustedEffectAdmissionPolicy>();
        (TrustedEffectSubmission submission, TrustedEffectContext context, TrustedEffectAdmission admission) = CreateEffect();
        _ = policy.PrepareAsync(submission, context, Arg.Any<CancellationToken>()).Returns(admission);
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
        TrustedEffectResult first = await actor.Actor.ProcessTrustedEffectAsync(submission, context, "test-proof");
        first.Disposition.ShouldBe(TrustedEffectDisposition.Success);

        // Model TTL expiry of every actor idempotency record before an actor restore.
        Dictionary<string, object> restoredView = state.CreateCommittedView()
            .Where(static entry => !entry.Key.StartsWith("idempotency:", StringComparison.Ordinal))
            .ToDictionary(static entry => entry.Key, static entry => entry.Value, StringComparer.Ordinal);
        state.CommittedState.Keys.ShouldContain(key => key.StartsWith("idempotency:", StringComparison.Ordinal));
        restoredView.Keys.ShouldNotContain(key => key.StartsWith("idempotency:", StringComparison.Ordinal));
        var restoredState = new FaultInjectingActorStateManager();
        await restoredState.SeedCommittedStateAsync(restoredView);
        IDomainServiceInvoker restoredInvoker = Substitute.For<IDomainServiceInvoker>();
        ActorTestContext restored = AggregateActorTestHelper.CreateActor(
            stateManager: restoredState,
            invoker: restoredInvoker,
            trustedEffectAdmissionPolicy: policy);

        TrustedEffectResult replay = await restored.Actor.ProcessTrustedEffectAsync(submission, context, "test-proof");

        replay.EffectId.ShouldBe(first.EffectId);
        replay.Disposition.ShouldBe(TrustedEffectDisposition.Success);
        replay.Replayed.ShouldBeTrue();
        _ = await restoredInvoker.DidNotReceiveWithAnyArgs().InvokeAsync(default!, default, default);
        _ = await restored.StatusStore.DidNotReceiveWithAnyArgs().ReadStatusAsync(default!, default!, default);
        restoredState.CommittedState.Keys.ShouldNotContain(key => key.Contains(":events:2", StringComparison.Ordinal));
        restoredState.CommittedState.Keys.ShouldNotContain(key => key.StartsWith("idempotency:", StringComparison.Ordinal));
    }

    /// <summary>Ordinary command processing cannot claim the reserved trusted-effect namespace.</summary>
    [Fact]
    public async Task OrdinaryCommandCannotUseReservedEffectNamespace()
    {
        var state = new FaultInjectingActorStateManager();
        ActorTestContext actor = AggregateActorTestHelper.CreateActor(stateManager: state);
        (TrustedEffectSubmission submission, _, _) = CreateEffect();
        Hexalith.EventStore.Contracts.Commands.CommandEnvelope command = AggregateActorTestHelper.CreateTestEnvelope() with
        {
            MessageId = submission.MessageId,
        };

        await Should.ThrowAsync<InvalidOperationException>(() => actor.Actor.ProcessCommandAsync(command));

        state.Trace.ShouldBeEmpty();
        _ = await actor.Invoker.DidNotReceiveWithAnyArgs().InvokeAsync(default!, default, default);
    }

    /// <summary>
    /// A same-aggregate effect whose causation is the source command's message is not a legacy
    /// replay of that command, so the target still commits its own receipt.
    /// </summary>
    [Fact]
    public async Task SameAggregateSourceCommandRecordDoesNotBlockEffectReceipt()
    {
        var state = new FaultInjectingActorStateManager();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await state.SeedCommittedStateAsync(new Dictionary<string, object>
        {
            [IdempotencyChecker.GetRecordKey("source-command")] = IdempotencyRecord.FromResult(
                new CommandProcessingIdentity("source-command", "source-command", "ScheduleResume"),
                new CommandProcessingResult(true, CorrelationId: "source-command"),
                now,
                now.AddHours(24),
                IdempotencyRecordDisposition.Terminal),
        });
        ITrustedEffectAdmissionPolicy policy = Substitute.For<ITrustedEffectAdmissionPolicy>();
        var identity = new EffectIdentity(
            "test-tenant", "test-domain", "agg-001", 1,
            EffectKindCatalog.DateResume, "test-domain", "agg-001", 0);
        string messageId = EffectIdentityCodec.ComputeMessageId(identity);
        var submission = new TrustedEffectSubmission(identity, "CreateOrder", [1, 2, 3], messageId, messageId);
        var context = new TrustedEffectContext("reactor", "date-resume", "source-command", "signed-token");
        _ = policy.PrepareAsync(submission, context, Arg.Any<CancellationToken>())
            .Returns(new TrustedEffectAdmission(submission, context, "DIGEST"));
        ActorTestContext actor = AggregateActorTestHelper.CreateActor(
            stateManager: state,
            trustedEffectAdmissionPolicy: policy);

        TrustedEffectResult result = await actor.Actor.ProcessTrustedEffectAsync(submission, context, "test-proof");

        result.Disposition.ShouldBe(TrustedEffectDisposition.NoOp);
        result.Replayed.ShouldBeFalse();
        state.CommittedState.Keys.ShouldContain("effect_receipt_" + result.EffectId);
        _ = await actor.Invoker.Received(1).InvokeAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Commands.CommandEnvelope>(),
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>An in-progress erasure closes the partition to ordinary commands.</summary>
    [Fact]
    public async Task OrdinaryCommandIsRejectedWhileErasureProgressExists()
    {
        var state = new FaultInjectingActorStateManager();
        await state.SeedCommittedStateAsync(new Dictionary<string, object>
        {
            [TrustedEffectErasureProgress.StateName] = new TrustedEffectErasureProgress(
                3, 2, Completed: false, new string('A', 32)),
        });
        ActorTestContext actor = AggregateActorTestHelper.CreateActor(stateManager: state);

        await Should.ThrowAsync<InvalidOperationException>(
            () => actor.Actor.ProcessCommandAsync(AggregateActorTestHelper.CreateTestEnvelope()));

        _ = await actor.Invoker.DidNotReceiveWithAnyArgs().InvokeAsync(default!, default, default);
        state.CommittedState.Keys.ShouldNotContain(key => key.Contains(":events:", StringComparison.Ordinal));
        state.CommittedState.Keys.ShouldNotContain(key => key.StartsWith("idempotency:", StringComparison.Ordinal));
    }

    /// <summary>
    /// A deletion fence alone leaves ordinary commands open. A failed deletion entry can leave a
    /// fence on an Active tenant, and only erasure progress closes the partition.
    /// </summary>
    [Fact]
    public async Task OrdinaryCommandIsAcceptedWhileOnlyDeletionFenceExists()
    {
        var state = new FaultInjectingActorStateManager();
        await state.SeedCommittedStateAsync(new Dictionary<string, object>
        {
            [TrustedEffectDeletionFence.StateName] = new TrustedEffectDeletionFence(
                "test-tenant", DateTimeOffset.UnixEpoch, "INVENTORY"),
        });
        ActorTestContext actor = AggregateActorTestHelper.CreateActor(stateManager: state);
        _ = actor.Invoker.InvokeAsync(
                Arg.Any<Hexalith.EventStore.Contracts.Commands.CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new AggregateActorTestHelper.TestEvent()]));

        CommandProcessingResult result = await actor.Actor.ProcessCommandAsync(
            AggregateActorTestHelper.CreateTestEnvelope());

        result.Accepted.ShouldBeTrue();
        _ = await actor.Invoker.Received(1).InvokeAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Commands.CommandEnvelope>(),
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());
        state.CommittedState.Keys.ShouldContain(key => key.Contains(":events:1", StringComparison.Ordinal));
        state.CommittedState.Keys.ShouldContain(TrustedEffectDeletionFence.StateName);
    }

    /// <summary>Fence and erasure denials are audited before any receipt read.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeletionDenialsAreAuditedBeforeReceiptAccess(bool erasing)
    {
        var state = new FaultInjectingActorStateManager();
        await state.SeedCommittedStateAsync(erasing
            ? new Dictionary<string, object>
            {
                [TrustedEffectErasureProgress.StateName] = new TrustedEffectErasureProgress(
                    0, 1, Completed: true, new string('A', 32)),
            }
            : new Dictionary<string, object>
            {
                [TrustedEffectDeletionFence.StateName] = new TrustedEffectDeletionFence(
                    "test-tenant", DateTimeOffset.UnixEpoch, "INVENTORY"),
            });
        ITrustedEffectAdmissionPolicy policy = Substitute.For<ITrustedEffectAdmissionPolicy>();
        ITrustedEffectAuditSink audit = Substitute.For<ITrustedEffectAuditSink>();
        (TrustedEffectSubmission submission, TrustedEffectContext context, TrustedEffectAdmission admission) = CreateEffect();
        _ = policy.PrepareAsync(submission, context, Arg.Any<CancellationToken>()).Returns(admission);
        ActorTestContext actor = AggregateActorTestHelper.CreateActor(
            stateManager: state,
            trustedEffectAdmissionPolicy: policy,
            trustedEffectAuditSink: audit);

        await Should.ThrowAsync<InvalidOperationException>(
            () => actor.Actor.ProcessTrustedEffectAsync(submission, context, "test-proof"));

        await audit.Received(1).AppendAsync(
            Arg.Is<TrustedEffectAuditRecord>(record => record.Action == (erasing ? "erasure-progress" : "deletion-fence")
                && record.Disposition == "denied"
                && record.Tenant == submission.Identity.Tenant
                && record.EffectId == EffectIdentityCodec.ComputeEffectId(submission.Identity)),
            Arg.Any<CancellationToken>());
        state.Trace.ShouldNotContain(entry => entry.StartsWith("TryGetState:effect_receipt_", StringComparison.Ordinal));
    }

    /// <summary>
    /// Field boundaries are part of the collision key, so attempts whose concatenated text is
    /// equal still leave two distinct quarantine records.
    /// </summary>
    [Fact]
    public async Task CollisionKeysSeparateAmbiguousFieldBoundaries()
    {
        var state = new FaultInjectingActorStateManager();
        ITrustedEffectAdmissionPolicy policy = Substitute.For<ITrustedEffectAdmissionPolicy>();
        (TrustedEffectSubmission submission, TrustedEffectContext context, TrustedEffectAdmission admission) = CreateEffect();
        _ = policy.PrepareAsync(submission, context, Arg.Any<CancellationToken>()).Returns(admission);
        ActorTestContext actor = AggregateActorTestHelper.CreateActor(
            stateManager: state,
            trustedEffectAdmissionPolicy: policy);
        _ = await actor.Actor.ProcessTrustedEffectAsync(submission, context, "test-proof");

        TrustedEffectContext first = context with { Workload = "b" };
        TrustedEffectContext second = context with { Workload = "ab" };
        _ = policy.PrepareAsync(submission, first, Arg.Any<CancellationToken>())
            .Returns(new TrustedEffectAdmission(submission, first, "CHANGEDa"));
        _ = policy.PrepareAsync(submission, second, Arg.Any<CancellationToken>())
            .Returns(new TrustedEffectAdmission(submission, second, "CHANGED"));
        await Should.ThrowAsync<InvalidOperationException>(
            () => actor.Actor.ProcessTrustedEffectAsync(submission, first, "test-proof"));
        await Should.ThrowAsync<InvalidOperationException>(
            () => actor.Actor.ProcessTrustedEffectAsync(submission, second, "test-proof"));

        state.CreateCommittedView().Keys
            .Count(static key => key.StartsWith("effect_collision_", StringComparison.Ordinal))
            .ShouldBe(2);
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
