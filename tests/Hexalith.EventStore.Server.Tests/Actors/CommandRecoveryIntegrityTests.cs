using System.Reflection;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class CommandRecoveryIntegrityTests
{
    private const string ActorIdentity = "test-tenant:test-domain:agg-001";

    private static CommandEnvelope Command(string messageId = "command-1", string? causationId = null)
        => new(messageId, "test-tenant", "test-domain", "agg-001", "CreateOrder", [1],
            "correlation-" + messageId, causationId, "system", null);

    private static (AggregateActor Actor, IDomainServiceInvoker Invoker, FakeEventPublisher Publisher, ActorTimerManager Timers)
        CreateActor(IActorStateManager state, ICommandStatusStore statuses, ActorTimerManager? timerManager = null,
            ILogger<AggregateActor>? logger = null)
    {
        var timers = timerManager ?? Substitute.For<ActorTimerManager>();
        var host = ActorHost.CreateForTest<AggregateActor>(new ActorTestOptions
        {
            ActorId = new ActorId(ActorIdentity), TimerManager = timers,
        });
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .Returns(DomainResult.Success([new CommandRecoveryEvent(), new CommandRecoveryEvent()]));
        var publisher = new FakeEventPublisher();
        var actor = new AggregateActor(host, logger ?? Substitute.For<ILogger<AggregateActor>>(), invoker,
            new FakeSnapshotManager(), new NoOpEventPayloadProtectionService(), statuses, publisher,
            Options.Create(new EventDrainOptions()), Options.Create(new BackpressureOptions()),
            Substitute.For<IDeadLetterPublisher>());
        ActorStateManagerTestHelper.SetStateManager(actor, state);
        return (actor, invoker, publisher, timers);
    }

    private static Task ActivateAsync(AggregateActor actor)
        => (Task)typeof(AggregateActor).GetMethod("OnActivateAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(actor, null)!;

    [Theory]
    [InlineData("status-write")]
    [InlineData("cleanup-before-commit")]
    [InlineData("cleanup-after-commit")]
    [InlineData("crash-after-terminal")]
    public async Task Committed_terminal_repairs_exact_status_after_recreation_without_reexecution(string fault)
    {
        var state = new FaultInjectingActorStateManager();
        var statuses = new InMemoryCommandStatusStore();
        ICommandStatusStore writer = Substitute.For<ICommandStatusStore>();
        var written = new List<CommandStatusRecord>();
        writer.WriteStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CommandStatusRecord>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                CommandStatusRecord status = call.ArgAt<CommandStatusRecord>(2);
                if (status.Status == CommandStatus.Completed)
                {
                    written.Add(status);
                    if (fault == "status-write") { throw new IOException("status unavailable"); }
                }
                await statuses.WriteStatusAsync(call.ArgAt<string>(0), call.ArgAt<string>(1), status);
            });
        if (fault == "cleanup-before-commit") { state.FaultOnCall("SaveState", 4, new IOException("cleanup unavailable")); }
        if (fault == "cleanup-after-commit") { state.FaultAfterCall("SaveState", 4, new IOException("cleanup acknowledgement lost")); }
        if (fault == "crash-after-terminal") { state.FaultAfterCall("SaveState", 3, new OperationCanceledException("crash")); }
        var context = CreateActor(state, writer);
        CommandEnvelope command = Command();
        if (fault == "crash-after-terminal")
        {
            await Should.ThrowAsync<OperationCanceledException>(() => context.Actor.ProcessCommandAsync(command));
        }
        else
        {
            (await context.Actor.ProcessCommandAsync(command)).Accepted.ShouldBeTrue();
        }

        var cached = (IdempotencyRecord)state.CommittedState["idempotency:command-1"];
        cached.Disposition.ShouldBe(IdempotencyRecordDisposition.Terminal);
        state.CommittedState.ContainsKey(ActorIdentity + ":pipeline:" + command.CorrelationId).ShouldBeFalse();
        ((UnpublishedPublicationIndex)state.CommittedState[UnpublishedPublicationIndex.StateKey]).Entries.ShouldBeEmpty();
        ((int)state.CommittedState["pending_command_count"]).ShouldBe(0);
        bool pending = fault != "cleanup-after-commit";
        state.CommittedState.ContainsKey(TerminalAdvisoryIntent.GetStateKey(command.MessageId)).ShouldBe(pending);
        TerminalAdvisoryIntent? intent = pending
            ? (TerminalAdvisoryIntent)state.CommittedState[TerminalAdvisoryIntent.GetStateKey(command.MessageId)] : null;
        if (intent is not null)
        {
            intent.StartSequence.ShouldBe(1);
            intent.EndSequence.ShouldBe(2);
            intent.Status.CommittedEventSequence.ShouldBe(2);
            if (written.Count > 0) { intent.Status.ShouldBe(written[0]); }
        }

        // Recreate the new persisted DTOs from the owning actor's default JSON options.
        var serialized = new Dictionary<string, object>(state.CommittedState);
        foreach ((string key, object value) in state.CommittedState)
        {
            if (value is TerminalAdvisoryIntent pendingIntent)
            {
                serialized[key] = JsonSerializer.Deserialize<TerminalAdvisoryIntent>(
                    JsonSerializer.Serialize(pendingIntent, JsonSerializerOptions.Web), JsonSerializerOptions.Web)!;
            }
            else if (value is TerminalAdvisoryRepairIndex pendingIndex)
            {
                serialized[key] = JsonSerializer.Deserialize<TerminalAdvisoryRepairIndex>(
                    JsonSerializer.Serialize(pendingIndex, JsonSerializerOptions.Web), JsonSerializerOptions.Web)!;
            }
        }
        state = new FaultInjectingActorStateManager();
        await state.SeedCommittedStateAsync(serialized);

        if (pending)
        {
            var conflicting = CreateActor(state, statuses);
            int writesBeforeConflict = statuses.GetStatusHistory(command.TenantId, command.MessageId).Count;
            CommandProcessingResult conflict = await conflicting.Actor.ProcessCommandAsync(command with { CausationId = "conflicting-causation" });
            conflict.Accepted.ShouldBeFalse();
            conflict.ErrorMessage.ShouldBe("command_identity_conflict");
            statuses.GetStatusHistory(command.TenantId, command.MessageId).Count.ShouldBe(writesBeforeConflict);
            state.CommittedState[TerminalAdvisoryIntent.GetStateKey(command.MessageId)].ShouldBe(intent);
            conflicting.Publisher.PublishCalls.ShouldBeEmpty();
            await conflicting.Invoker.DidNotReceive().InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>());
        }

        // A separate command advances the head while the original advisory remains pending.
        var healthy = CreateActor(state, statuses);
        (await healthy.Actor.ProcessCommandAsync(Command("command-2"))).Accepted.ShouldBeTrue();
        ((AggregateMetadata)state.CommittedState[ActorIdentity + ":metadata"]).CurrentSequence.ShouldBe(4);
        var recreated = CreateActor(state, statuses);
        CommandProcessingResult duplicate = await recreated.Actor.ProcessCommandAsync(command with { CorrelationId = "redelivery-correlation" });
        duplicate.ShouldBe(cached.ToResult());
        CommandStatusRecord completed = (await statuses.ReadStatusAsync(command.TenantId, command.MessageId)).ShouldNotBeNull();
        completed.CommittedEventSequence.ShouldBe(2);
        if (intent is not null) { completed.ShouldBe(intent.Status); }
        recreated.Publisher.PublishCalls.ShouldBeEmpty();
        await recreated.Invoker.DidNotReceive().InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>());
        state.CommittedState.ContainsKey(TerminalAdvisoryIntent.GetStateKey(command.MessageId)).ShouldBeFalse();
        ((TerminalAdvisoryRepairIndex)state.CommittedState[TerminalAdvisoryRepairIndex.StateKey]).Entries.ShouldBeEmpty();
        int writes = statuses.GetStatusHistory(command.TenantId, command.MessageId).Count;
        (await recreated.Actor.ProcessCommandAsync(command)).ShouldBe(duplicate);
        statuses.GetStatusHistory(command.TenantId, command.MessageId).Count.ShouldBe(writes);
    }

    [Fact]
    public async Task Fresh_omitted_causation_survives_crash_before_terminal_commit_and_resumes_exact_range()
    {
        var state = new FaultInjectingActorStateManager();
        state.FaultOnCall("SaveState", 3, new OperationCanceledException("crash before terminal"));
        var statuses = new InMemoryCommandStatusStore();
        var original = CreateActor(state, statuses);
        CommandEnvelope command = Command();
        await Should.ThrowAsync<OperationCanceledException>(() => original.Actor.ProcessCommandAsync(command));
        var checkpoint = (PipelineState)state.CommittedState[ActorIdentity + ":pipeline:" + command.CorrelationId];
        checkpoint.CausationId.ShouldBe(command.MessageId);
        state.CommittedState.Values.OfType<EventEnvelope>().ShouldAllBe(e => e.CausationId == checkpoint.CausationId);
        state.CommittedState.ContainsKey("idempotency:command-1").ShouldBeFalse();
        state.CommittedState.ContainsKey(TerminalAdvisoryIntent.GetStateKey(command.MessageId)).ShouldBeFalse();
        var recreated = CreateActor(state, statuses);
        (await recreated.Actor.ProcessCommandAsync(command)).Accepted.ShouldBeTrue();
        recreated.Publisher.PublishCalls.ShouldHaveSingleItem().Events.Select(e => e.SequenceNumber).ShouldBe(new long[] { 1, 2 });
        await recreated.Invoker.DidNotReceive().InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>());
        ((IdempotencyRecord)state.CommittedState["idempotency:command-1"]).Disposition.ShouldBe(IdempotencyRecordDisposition.Terminal);
    }

    [Theory]
    [InlineData("missing-event")]
    [InlineData("tenant")]
    [InlineData("domain")]
    [InlineData("aggregate")]
    [InlineData("sequence")]
    [InlineData("correlation")]
    [InlineData("causation")]
    [InlineData("missing-message")]
    [InlineData("missing-causation")]
    [InlineData("missing-count")]
    [InlineData("negative-count")]
    [InlineData("zero-with-range")]
    [InlineData("missing-type")]
    [InlineData("checkpoint-correlation")]
    [InlineData("missing-range")]
    [InlineData("wrong-count")]
    [InlineData("reader-boundary")]
    public async Task Unverifiable_checkpoint_retains_durable_evidence_on_repeated_resume_and_activation_then_restores(string defect)
    {
        var state = new FaultInjectingActorStateManager();
        var statuses = new InMemoryCommandStatusStore();
        CommandEnvelope command = Command();
        var trustedCheckpoint = new PipelineState(command.CorrelationId, CommandStatus.EventsStored, command.CommandType,
            DateTimeOffset.UtcNow, 2, null, MessageId: command.MessageId, CausationId: command.MessageId,
            StartSequence: 7, EndSequence: 8);
        PipelineState checkpoint = defect switch
        {
            "missing-message" => trustedCheckpoint with { MessageId = null },
            "missing-causation" => trustedCheckpoint with { CausationId = null },
            "missing-count" => trustedCheckpoint with { EventCount = null },
            "negative-count" => trustedCheckpoint with { EventCount = -1 },
            "zero-with-range" => trustedCheckpoint with { EventCount = 0 },
            "missing-type" => trustedCheckpoint with { CommandType = "" },
            "checkpoint-correlation" => trustedCheckpoint with { CorrelationId = "other" },
            "missing-range" => trustedCheckpoint with { StartSequence = null },
            "wrong-count" => trustedCheckpoint with { EventCount = 3 },
            "reader-boundary" => trustedCheckpoint with { StartSequence = int.MaxValue, EndSequence = (long)int.MaxValue + 1 },
            _ => trustedCheckpoint,
        };
        var trusted = new Dictionary<string, object>
        {
            [ActorIdentity + ":pipeline:" + command.CorrelationId] = trustedCheckpoint,
            [UnpublishedPublicationIndex.StateKey] = new UnpublishedPublicationIndex([
                new(command.MessageId, command.CorrelationId, DateTimeOffset.UtcNow),
                new("other-owner", "other-correlation", DateTimeOffset.UtcNow)]),
            ["pending_command_count"] = 2,
            ["drain:other-owner"] = new UnpublishedEventsRecord("other-correlation", 1, 1, 1,
                "OtherCommand", false, DateTimeOffset.UtcNow, 0, null, "other-owner", ReminderArmedAt: DateTimeOffset.UtcNow),
            [ActorIdentity + ":metadata"] = new AggregateMetadata(30, DateTimeOffset.UtcNow, null),
            ["test-tenant:test-domain:other-aggregate:metadata"] = new AggregateMetadata(11, DateTimeOffset.UtcNow, null),
        };
        for (int sequence = 7; sequence <= 8; sequence++)
        {
            trusted[ActorIdentity + ":events:" + sequence] = new EventEnvelope(Guid.NewGuid().ToString(), command.AggregateId,
                command.Domain, command.TenantId, command.Domain, sequence, 0, DateTimeOffset.UtcNow,
                command.CorrelationId, command.MessageId, "system", "1.0.0", "Created", 1, "json", [1], null);
        }
        var corrupt = new Dictionary<string, object>(trusted)
        {
            [ActorIdentity + ":pipeline:" + command.CorrelationId] = checkpoint,
        };
        EventEnvelope correct = (EventEnvelope)trusted[ActorIdentity + ":events:7"];
        // Event envelopes are immutable; reconstruct one differing metadata field as stored corruption.
        corrupt[ActorIdentity + ":events:7"] = new EventEnvelope(correct.MessageId,
            defect == "aggregate" ? "other" : correct.AggregateId, correct.AggregateType,
            defect == "tenant" ? "other" : correct.TenantId, defect == "domain" ? "other" : correct.Domain,
            defect == "sequence" ? 6 : correct.SequenceNumber, correct.GlobalPosition, correct.Timestamp,
            defect == "correlation" ? "other" : correct.CorrelationId,
            defect == "causation" ? "other" : correct.CausationId, correct.UserId, correct.DomainServiceVersion,
            correct.EventTypeName, correct.MetadataVersion, correct.SerializationFormat, correct.Payload, correct.Extensions);
        if (defect == "missing-event") { corrupt.Remove(ActorIdentity + ":events:7"); }
        await state.SeedCommittedStateAsync(corrupt);
        var context = CreateActor(state, statuses);
        for (int retry = 0; retry < 3; retry++)
        {
            CommandProcessingResult refused = await context.Actor.ProcessCommandAsync(command);
            refused.Accepted.ShouldBeFalse();
            refused.ErrorMessage.ShouldBe("command_identity_conflict");
            refused.CorrelationId.ShouldBe(command.CorrelationId);
            await ActivateAsync(context.Actor);
            state.CommittedState.Keys.Order().ShouldBe(corrupt.Keys.Order());
            foreach ((string key, object value) in corrupt) { state.CommittedState[key].ShouldBe(value); }
        }
        context.Publisher.PublishCalls.ShouldBeEmpty();
        await context.Invoker.DidNotReceive().InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>());
        state.Trace.ShouldNotContain("SaveState");

        // Model an independently authorized exact restore through the owning actor-state seam.
        await state.SeedCommittedStateAsync(trusted);
        var restored = CreateActor(state, statuses);
        (await restored.Actor.ProcessCommandAsync(command)).Accepted.ShouldBeTrue();
        restored.Publisher.PublishCalls.ShouldHaveSingleItem().Events.Select(e => e.SequenceNumber).ShouldBe(new long[] { 7, 8 });
        ((UnpublishedPublicationIndex)state.CommittedState[UnpublishedPublicationIndex.StateKey]).Entries.ShouldHaveSingleItem().MessageId.ShouldBe("other-owner");
        state.CommittedState["drain:other-owner"].ShouldBe(trusted["drain:other-owner"]);
        state.CommittedState["test-tenant:test-domain:other-aggregate:metadata"].ShouldBe(trusted["test-tenant:test-domain:other-aggregate:metadata"]);
        ((int)state.CommittedState["pending_command_count"]).ShouldBe(1);
        (await statuses.ReadStatusAsync(command.TenantId, command.MessageId))!.CommittedEventSequence.ShouldBe(8);
    }

    [Fact]
    public async Task Activation_bounds_independent_advisory_reminders_and_skips_armed_head_without_writes()
    {
        var state = new FaultInjectingActorStateManager();
        var statuses = new InMemoryCommandStatusStore();
        var seed = new Dictionary<string, object>();
        var entries = new List<TerminalAdvisoryRepairEntry>();
        for (int index = 0; index < 12; index++)
        {
            string messageId = "pending-" + index;
            entries.Add(new(messageId));
            seed[TerminalAdvisoryIntent.GetStateKey(messageId)] = new TerminalAdvisoryIntent("test-tenant", "test-domain", "agg-001",
                messageId, "corr-" + index, messageId, "CreateOrder", 0, null, null,
                new CommandStatusRecord(CommandStatus.Completed, DateTimeOffset.UtcNow, "agg-001", 0, null, null, null,
                    messageId, "corr-" + index) { Domain = "test-domain" });
        }
        seed[TerminalAdvisoryRepairIndex.StateKey] = new TerminalAdvisoryRepairIndex(entries);
        await state.SeedCommittedStateAsync(seed);
        var context = CreateActor(state, statuses);
        await ActivateAsync(context.Actor);
        ((TerminalAdvisoryRepairIndex)state.CommittedState[TerminalAdvisoryRepairIndex.StateKey]).Entries.Count(e => e.ReminderArmedAt is not null).ShouldBe(8);
        await ActivateAsync(context.Actor);
        ((TerminalAdvisoryRepairIndex)state.CommittedState[TerminalAdvisoryRepairIndex.StateKey]).Entries.ShouldAllBe(e => e.ReminderArmedAt != null);
        statuses.GetStatusHistory("test-tenant", "pending-0").ShouldBeEmpty();
        context.Publisher.PublishCalls.ShouldBeEmpty();
        await context.Invoker.DidNotReceive().InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>());
        await context.Actor.ReceiveReminderAsync(TerminalAdvisoryIntent.GetReminderName("pending-11"), [], TimeSpan.Zero, TimeSpan.Zero);
        state.CommittedState.ContainsKey(TerminalAdvisoryIntent.GetStateKey("pending-11")).ShouldBeFalse();
        state.CommittedState.ContainsKey(TerminalAdvisoryIntent.GetStateKey("pending-0")).ShouldBeTrue();
        ((TerminalAdvisoryRepairIndex)state.CommittedState[TerminalAdvisoryRepairIndex.StateKey]).Entries.Count.ShouldBe(11);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Noop_and_rejection_pending_advisories_repair_without_sequence_proof(bool rejection)
    {
        var state = new FaultInjectingActorStateManager();
        ICommandStatusStore broken = Substitute.For<ICommandStatusStore>();
        broken.WriteStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CommandStatusRecord>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<CommandStatusRecord>(2).Status is CommandStatus.Completed or CommandStatus.Rejected
                ? Task.FromException(new IOException("status unavailable")) : Task.CompletedTask);
        var original = CreateActor(state, broken);
        original.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .Returns(rejection ? DomainResult.Rejection([new CommandRecoveryRejectionEvent()]) : DomainResult.NoOp());
        CommandEnvelope command = Command();
        CommandProcessingResult result = await original.Actor.ProcessCommandAsync(command);
        var intent = (TerminalAdvisoryIntent)state.CommittedState[TerminalAdvisoryIntent.GetStateKey(command.MessageId)];
        intent.Status.Status.ShouldBe(rejection ? CommandStatus.Rejected : CommandStatus.Completed);
        intent.Status.CommittedEventSequence.ShouldBeNull();
        intent.EventCount.ShouldBe(rejection ? 1 : 0);
        var statuses = new InMemoryCommandStatusStore();
        var recreated = CreateActor(state, statuses);
        (await recreated.Actor.ProcessCommandAsync(command)).ShouldBe(result);
        (await statuses.ReadStatusAsync(command.TenantId, command.MessageId)).ShouldBe(intent.Status);
        recreated.Publisher.PublishCalls.ShouldBeEmpty();
        await recreated.Invoker.DidNotReceive().InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>());
        state.CommittedState.ContainsKey(TerminalAdvisoryIntent.GetStateKey(command.MessageId)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Successful_drain_retains_exact_terminal_advisory_after_status_failure_or_crash(bool rejection, bool crash)
    {
        var state = new FaultInjectingActorStateManager();
        CommandEnvelope command = Command(causationId: "cause-command");
        var record = new UnpublishedEventsRecord(command.CorrelationId, 7, 8, 2, command.CommandType,
            rejection, DateTimeOffset.UtcNow, 2, "publish failed", command.MessageId, CausationId: command.CausationId);
        await state.SeedCommittedStateAsync(new Dictionary<string, object>
        {
            [UnpublishedEventsRecord.GetStateKey(command.MessageId)] = record,
            [UnpublishedPublicationIndex.StateKey] = new UnpublishedPublicationIndex([new(command.MessageId, command.CorrelationId, DateTimeOffset.UtcNow)]),
            ["pending_command_count"] = 1,
            [ActorIdentity + ":events:7"] = new EventEnvelope(Guid.NewGuid().ToString(), "agg-001", "test-domain", "test-tenant", "test-domain",
                7, 0, DateTimeOffset.UtcNow, command.CorrelationId, command.CausationId!, "system", "1.0.0", "Created", 1, "json", [1], null),
            [ActorIdentity + ":events:8"] = new EventEnvelope(Guid.NewGuid().ToString(), "agg-001", "test-domain", "test-tenant", "test-domain",
                8, 0, DateTimeOffset.UtcNow, command.CorrelationId, command.CausationId!, "system", "1.0.0", "Created", 1, "json", [1], null),
        });
        ICommandStatusStore broken = Substitute.For<ICommandStatusStore>();
        broken.WriteStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CommandStatusRecord>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("status unavailable")));
        var original = CreateActor(state, broken);
        if (crash)
        {
            state.FaultAfterCall("SaveState", 1, new OperationCanceledException("crash after drain commit"));
            await Should.ThrowAsync<OperationCanceledException>(() => original.Actor.ReceiveReminderAsync(
                UnpublishedEventsRecord.GetReminderName(command.MessageId), [], TimeSpan.Zero, TimeSpan.Zero));
        }
        else
        {
            await original.Actor.ReceiveReminderAsync(UnpublishedEventsRecord.GetReminderName(command.MessageId), [], TimeSpan.Zero, TimeSpan.Zero);
        }
        state.CommittedState.ContainsKey(UnpublishedEventsRecord.GetStateKey(command.MessageId)).ShouldBeFalse();
        ((UnpublishedPublicationIndex)state.CommittedState[UnpublishedPublicationIndex.StateKey]).Entries.ShouldBeEmpty();
        state.CommittedState["pending_command_count"].ShouldBe(0);
        var intent = (TerminalAdvisoryIntent)state.CommittedState[TerminalAdvisoryIntent.GetStateKey(command.MessageId)];
        intent.Status.CommittedEventSequence.ShouldBe(rejection ? null : 8);
        intent.Status.DrainAttemptCount.ShouldBe(2);
        var statuses = new InMemoryCommandStatusStore();
        var recreated = CreateActor(state, statuses);
        await ActivateAsync(recreated.Actor);
        recreated.Publisher.PublishCalls.ShouldBeEmpty();
        await recreated.Actor.ReceiveReminderAsync(TerminalAdvisoryIntent.GetReminderName(command.MessageId), [], TimeSpan.Zero, TimeSpan.Zero);
        (await statuses.ReadStatusAsync(command.TenantId, command.MessageId)).ShouldBe(intent.Status);
        recreated.Publisher.PublishCalls.ShouldBeEmpty();
        await recreated.Invoker.DidNotReceive().InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>());
        state.CommittedState.ContainsKey(TerminalAdvisoryIntent.GetStateKey(command.MessageId)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("domain")]
    [InlineData("aggregate")]
    public async Task Incoming_command_scope_must_equal_actor_partition_before_recovery(string scope)
    {
        var state = new FaultInjectingActorStateManager();
        var statuses = new InMemoryCommandStatusStore();
        CommandEnvelope incoming = scope == "domain" ? Command() with { Domain = "other" } : Command() with { AggregateId = "other" };
        string key = incoming.AggregateIdentity.PipelineKeyPrefix + incoming.CorrelationId;
        var checkpoint = new PipelineState(incoming.CorrelationId, CommandStatus.EventsStored, incoming.CommandType,
            DateTimeOffset.UtcNow, 0, null, MessageId: incoming.MessageId, CausationId: incoming.MessageId);
        await state.SeedCommittedStateAsync(new Dictionary<string, object> { [key] = checkpoint });
        var context = CreateActor(state, statuses);
        CommandProcessingResult result = await context.Actor.ProcessCommandAsync(incoming);
        result.Accepted.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("command_identity_conflict");
        context.Publisher.PublishCalls.ShouldBeEmpty();
        await context.Invoker.DidNotReceive().InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>());
        state.CommittedState[key].ShouldBe(checkpoint);
        state.Trace.ShouldNotContain("SaveState");
    }

    [Theory]
    [InlineData("proof")]
    [InlineData("count")]
    [InlineData("scope")]
    [InlineData("noop-range")]
    public async Task Malformed_pending_advisory_is_retained_without_asserting_unverifiable_completion(string defect)
    {
        var state = new FaultInjectingActorStateManager();
        var statuses = new InMemoryCommandStatusStore();
        CommandEnvelope command = Command();
        var status = new CommandStatusRecord(CommandStatus.Completed, DateTimeOffset.UtcNow, command.AggregateId,
            defect == "count" ? 3 : 2, null, null, null, command.MessageId, command.CorrelationId)
        {
            Domain = command.Domain,
            CommittedEventSequence = defect == "proof" ? 8 : 2,
        };
        var intent = new TerminalAdvisoryIntent(command.TenantId, defect == "scope" ? "other" : command.Domain,
            command.AggregateId, command.MessageId, command.CorrelationId, command.MessageId, command.CommandType,
            defect == "noop-range" ? 0 : 2, 1, 2, status);
        var index = new TerminalAdvisoryRepairIndex([new(command.MessageId)]);
        await state.SeedCommittedStateAsync(new Dictionary<string, object>
        {
            [TerminalAdvisoryIntent.GetStateKey(command.MessageId)] = intent,
            [TerminalAdvisoryRepairIndex.StateKey] = index,
            ["idempotency:" + command.MessageId] = new IdempotencyRecord(command.MessageId, command.CorrelationId, true, null,
                DateTimeOffset.UtcNow, 2, MessageId: command.MessageId, CommandType: command.CommandType,
                ExpiresAt: DateTimeOffset.UtcNow.AddHours(1), Disposition: IdempotencyRecordDisposition.Terminal),
        });
        var context = CreateActor(state, statuses);
        await ActivateAsync(context.Actor);
        await context.Actor.ReceiveReminderAsync(TerminalAdvisoryIntent.GetReminderName(command.MessageId), [], TimeSpan.Zero, TimeSpan.Zero);
        (await context.Actor.ProcessCommandAsync(command)).Accepted.ShouldBeTrue();
        statuses.GetStatusHistory(command.TenantId, command.MessageId).ShouldBeEmpty();
        state.CommittedState[TerminalAdvisoryIntent.GetStateKey(command.MessageId)].ShouldBe(intent);
        state.CommittedState[TerminalAdvisoryRepairIndex.StateKey].ShouldBe(index);
        state.Trace.ShouldNotContain("SaveState");
        context.Publisher.PublishCalls.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Persisted_recovery_read_failure_retains_checkpoint_and_retries_without_terminal_classification(bool cancellation)
    {
        var state = new FaultInjectingActorStateManager();
        var statuses = new InMemoryCommandStatusStore();
        CommandEnvelope command = Command();
        var checkpoint = new PipelineState(command.CorrelationId, CommandStatus.EventsStored, command.CommandType,
            DateTimeOffset.UtcNow, 1, null, MessageId: command.MessageId, CausationId: command.MessageId,
            StartSequence: 7, EndSequence: 7);
        var seed = new Dictionary<string, object>
        {
            [ActorIdentity + ":pipeline:" + command.CorrelationId] = checkpoint,
            [ActorIdentity + ":events:7"] = new EventEnvelope(Guid.NewGuid().ToString(), "agg-001", "test-domain", "test-tenant", "test-domain",
                7, 0, DateTimeOffset.UtcNow, command.CorrelationId, command.MessageId, "system", "1.0.0", "Created", 1, "json", [1], null),
        };
        await state.SeedCommittedStateAsync(seed);
        state.FaultOnCall("TryGetState:" + ActorIdentity + ":events:7", 1,
            cancellation ? new OperationCanceledException("read cancelled") : new IOException("provider unavailable"));
        var context = CreateActor(state, statuses);
        if (cancellation)
        {
            await Should.ThrowAsync<OperationCanceledException>(() => context.Actor.ProcessCommandAsync(command));
        }
        else
        {
            await Should.ThrowAsync<DrainStateStoreException>(() => context.Actor.ProcessCommandAsync(command));
        }
        context.Publisher.PublishCalls.ShouldBeEmpty();
        state.Trace.ShouldNotContain("SaveState");
        state.CommittedState.Keys.Order().ShouldBe(seed.Keys.Order());
        foreach ((string key, object value) in seed) { state.CommittedState[key].ShouldBe(value); }
        statuses.GetStatusHistory(command.TenantId, command.MessageId).ShouldBeEmpty();
        (await context.Actor.ProcessCommandAsync(command)).Accepted.ShouldBeTrue();
        context.Publisher.PublishCalls.ShouldHaveSingleItem().Events.ShouldHaveSingleItem().SequenceNumber.ShouldBe(7);
        await context.Invoker.DidNotReceive().InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>());
    }

    [Theory]
    [InlineData("count")]
    [InlineData("missing-event")]
    [InlineData("causation")]
    [InlineData("scope")]
    [InlineData("legacy")]
    public async Task Unverifiable_drain_retains_evidence_and_retry_budget_even_at_exhaustion(string defect)
    {
        var state = new FaultInjectingActorStateManager();
        var statuses = new InMemoryCommandStatusStore();
        CommandEnvelope command = Command();
        var trustedRecord = new UnpublishedEventsRecord(command.CorrelationId, 7, 7, 1, command.CommandType,
            false, DateTimeOffset.UtcNow, 0, null, command.MessageId, CausationId: command.MessageId);
        var trustedEvent = new EventEnvelope(Guid.NewGuid().ToString(), "agg-001", "test-domain", "test-tenant", "test-domain",
            7, 0, DateTimeOffset.UtcNow, command.CorrelationId, command.MessageId, "system", "1.0.0", "Created", 1, "json", [1], null);
        var record = trustedRecord with
        {
            RetryCount = EventDrainOptions.DefaultMaxDrainAttempts,
            EventCount = defect == "count" ? 2 : 1,
            CausationId = defect == "legacy" ? null : defect == "causation" ? "other" : command.MessageId,
        };
        var seed = new Dictionary<string, object>
        {
            [UnpublishedEventsRecord.GetStateKey(command.MessageId)] = record,
            [UnpublishedPublicationIndex.StateKey] = new UnpublishedPublicationIndex([new(command.MessageId, command.CorrelationId, DateTimeOffset.UtcNow)]),
            ["pending_command_count"] = 1,
            [ActorIdentity + ":events:7"] = defect == "scope" ? trustedEvent with { AggregateId = "other" } : trustedEvent,
            ["test-tenant:test-domain:other-aggregate:metadata"] = new AggregateMetadata(2, DateTimeOffset.UtcNow, null),
        };
        if (defect == "missing-event") { seed.Remove(ActorIdentity + ":events:7"); }
        await state.SeedCommittedStateAsync(seed);
        var context = CreateActor(state, statuses);
        for (int retry = 0; retry < 3; retry++)
        {
            await context.Actor.ReceiveReminderAsync(UnpublishedEventsRecord.GetReminderName(command.MessageId), [], TimeSpan.Zero, TimeSpan.Zero);
            await ActivateAsync(context.Actor);
            state.CommittedState.Keys.Order().ShouldBe(seed.Keys.Order());
            foreach ((string key, object value) in seed) { state.CommittedState[key].ShouldBe(value); }
        }
        state.Trace.ShouldNotContain("SaveState");
        context.Publisher.PublishCalls.ShouldBeEmpty();
        statuses.GetStatusHistory(command.TenantId, command.MessageId).ShouldBeEmpty();
        // Trusted backup restoration makes the exact original range verifiable again.
        seed[UnpublishedEventsRecord.GetStateKey(command.MessageId)] = trustedRecord;
        seed[ActorIdentity + ":events:7"] = trustedEvent;
        await state.SeedCommittedStateAsync(seed);
        var restored = CreateActor(state, statuses);
        await restored.Actor.ReceiveReminderAsync(UnpublishedEventsRecord.GetReminderName(command.MessageId), [], TimeSpan.Zero, TimeSpan.Zero);
        restored.Publisher.PublishCalls.ShouldHaveSingleItem().Events.ShouldHaveSingleItem().SequenceNumber.ShouldBe(7);
        state.CommittedState.ContainsKey(UnpublishedEventsRecord.GetStateKey(command.MessageId)).ShouldBeFalse();
        state.CommittedState["test-tenant:test-domain:other-aggregate:metadata"].ShouldBe(seed["test-tenant:test-domain:other-aggregate:metadata"]);
    }

    private static TerminalAdvisoryIntent ReviewIntent(string messageId, long start = 1, int count = 1)
        => new("test-tenant", "test-domain", "agg-001", messageId, "correlation-" + messageId, messageId,
            "CreateOrder", count, start, start + count - 1,
            new CommandStatusRecord(CommandStatus.Completed, DateTimeOffset.UnixEpoch, "agg-001", count,
                null, null, null, messageId, "correlation-" + messageId)
            {
                Domain = "test-domain", CommittedEventSequence = start + count - 1,
            });

    private static IdempotencyRecord ReviewTerminal(TerminalAdvisoryIntent intent)
        => new(intent.CausationId, intent.CorrelationId, true, null, DateTimeOffset.UtcNow, intent.EventCount,
            MessageId: intent.MessageId, CommandType: intent.CommandType, ExpiresAt: DateTimeOffset.UtcNow.AddHours(1),
            Disposition: IdempotencyRecordDisposition.Terminal);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Bounded_discovery_passes_invalid_or_failed_prefix_after_actor_recreation(bool advisory, bool readFailure)
    {
        var state = new FaultInjectingActorStateManager();
        var statuses = new InMemoryCommandStatusStore();
        var timer = new RegisteredReminderHarness();
        var entries = new List<UnpublishedPublicationEntry>();
        var advisoryEntries = new List<TerminalAdvisoryRepairEntry>();
        var seed = new Dictionary<string, object>();
        for (int owner = 0; owner <= 32; owner++)
        {
            string messageId = "owner-" + owner;
            entries.Add(new(messageId, "correlation-" + messageId, DateTimeOffset.UnixEpoch));
            advisoryEntries.Add(new(messageId));
            if (advisory)
            {
                seed[TerminalAdvisoryIntent.GetStateKey(messageId)] = owner == 32
                    ? ReviewIntent(messageId) : ReviewIntent(messageId) with { EventCount = -1 };
            }
            else
            {
                seed[UnpublishedEventsRecord.GetStateKey(messageId)] = new UnpublishedEventsRecord("correlation-" + messageId,
                    1, 1, owner == 32 ? 1 : 2, "CreateOrder", false, DateTimeOffset.UnixEpoch, 0, null,
                    messageId, CausationId: messageId);
            }
        }
        seed["unrelated-aggregate-evidence"] = "unchanged";
        if (advisory) { seed[TerminalAdvisoryRepairIndex.StateKey] = new TerminalAdvisoryRepairIndex(advisoryEntries); }
        else
        {
            seed[UnpublishedPublicationIndex.StateKey] = new UnpublishedPublicationIndex(entries);
            seed["pending_command_count"] = 33;
            seed[ActorIdentity + ":events:1"] = new EventEnvelope(Guid.NewGuid().ToString(), "agg-001", "test-domain", "test-tenant", "test-domain",
                1, 0, DateTimeOffset.UnixEpoch, "correlation-owner-32", "owner-32", "system", "1.0.0", "Created", 1, "json", [1], null);
        }
        await state.SeedCommittedStateAsync(seed);
        if (readFailure)
        {
            for (int owner = 0; owner < 32; owner++)
            {
                string key = advisory ? TerminalAdvisoryIntent.GetStateKey("owner-" + owner) : UnpublishedEventsRecord.GetStateKey("owner-" + owner);
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    state.FaultOnCall("TryGetState:" + key, attempt, new IOException("private-provider-secret"));
                }
            }
        }
        var original = CreateActor(state, statuses, timer.Manager);
        await ActivateAsync(original.Actor);
        timer.RegisteredNames.ShouldBeEmpty();
        string cursorKey = advisory ? RecoveryScanProgress.AdvisoryKey : RecoveryScanProgress.PublicationKey;
        var cursor = (RecoveryScanProgress)state.CommittedState[cursorKey];
        cursor.NextOffset.ShouldBe(32);
        JsonSerializer.Deserialize<RecoveryScanProgress>(JsonSerializer.Serialize(cursor, JsonSerializerOptions.Web), JsonSerializerOptions.Web).ShouldBe(cursor);
        var recreated = CreateActor(state, statuses, timer.Manager);
        await ActivateAsync(recreated.Actor);
        timer.RegisteredNames.ShouldHaveSingleItem().ShouldBe(advisory
            ? TerminalAdvisoryIntent.GetReminderName("owner-32") : UnpublishedEventsRecord.GetReminderName("owner-32"));
        for (int owner = 0; owner < 32; owner++)
        {
            string key = advisory ? TerminalAdvisoryIntent.GetStateKey("owner-" + owner) : UnpublishedEventsRecord.GetStateKey("owner-" + owner);
            state.CommittedState[key].ShouldBe(seed[key]);
        }
        if (advisory) { ((TerminalAdvisoryRepairIndex)state.CommittedState[TerminalAdvisoryRepairIndex.StateKey]).Entries.Count.ShouldBe(33); }
        else
        {
            ((UnpublishedPublicationIndex)state.CommittedState[UnpublishedPublicationIndex.StateKey]).Entries.ShouldBe(entries);
            state.CommittedState["pending_command_count"].ShouldBe(33);
        }
        state.CommittedState["unrelated-aggregate-evidence"].ShouldBe("unchanged");
        recreated.Publisher.PublishCalls.ShouldBeEmpty();
        await recreated.Invoker.DidNotReceive().InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>());
    }

    [Theory]
    [InlineData("key")]
    [InlineData("accepted")]
    [InlineData("rejection-type")]
    [InlineData("recoverable")]
    [InlineData("intent-rejected")]
    [InlineData("rejection-evidence")]
    public async Task Registered_advisory_refuses_key_or_terminal_result_contradictions(string contradiction)
    {
        var state = new FaultInjectingActorStateManager();
        var statuses = new InMemoryCommandStatusStore();
        var timer = new RegisteredReminderHarness();
        TerminalAdvisoryIntent intent = ReviewIntent("command-1");
        var seed = new Dictionary<string, object>
        {
            [TerminalAdvisoryIntent.GetStateKey(intent.MessageId)] = intent,
            [TerminalAdvisoryRepairIndex.StateKey] = new TerminalAdvisoryRepairIndex([new(intent.MessageId)]),
            [IdempotencyChecker.GetRecordKey(intent.MessageId)] = ReviewTerminal(intent),
        };
        await state.SeedCommittedStateAsync(seed);
        var context = CreateActor(state, statuses, timer.Manager);
        await ActivateAsync(context.Actor);
        timer.RegisteredNames.ShouldHaveSingleItem();
        var corrupted = new Dictionary<string, object>(state.CommittedState);
        IdempotencyRecord cached = ReviewTerminal(intent);
        if (contradiction == "key") { corrupted[TerminalAdvisoryIntent.GetStateKey(intent.MessageId)] = ReviewIntent("other-command"); }
        if (contradiction == "accepted") { cached = cached with { Accepted = false, RejectionEventType = "Rejected" }; }
        if (contradiction == "rejection-type") { cached = cached with { RejectionEventType = "Rejected" }; }
        if (contradiction == "recoverable") { cached = cached with { Disposition = IdempotencyRecordDisposition.Recoverable }; }
        if (contradiction is "intent-rejected" or "rejection-evidence")
        {
            corrupted[TerminalAdvisoryIntent.GetStateKey(intent.MessageId)] = intent with
            {
                Status = intent.Status with { Status = CommandStatus.Rejected, RejectionEventType = "OtherRejection", CommittedEventSequence = null },
            };
            if (contradiction == "rejection-evidence") { cached = cached with { Accepted = false, RejectionEventType = "OriginalRejection" }; }
        }
        corrupted[IdempotencyChecker.GetRecordKey(intent.MessageId)] = cached;
        await state.SeedCommittedStateAsync(corrupted);
        (await context.Actor.ProcessCommandAsync(Command())).ShouldBe(cached.ToResult());
        await timer.AdvanceAsync(context.Actor, TimeSpan.FromMinutes(1));
        await ActivateAsync(CreateActor(state, statuses, timer.Manager).Actor);
        statuses.GetStatusHistory(intent.TenantId, intent.MessageId).ShouldBeEmpty();
        foreach ((string key, object value) in corrupted) { state.CommittedState[key].ShouldBe(value); }
        state.Trace.ShouldNotContain("SaveState");
        context.Publisher.PublishCalls.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registered_scheduler_repairs_exact_status_after_registration_and_acknowledgement_faults(bool acknowledgementAfterCommit)
    {
        var state = new FaultInjectingActorStateManager();
        var timer = new RegisteredReminderHarness { FailRegistration = true };
        var statuses = new InMemoryCommandStatusStore();
        ICommandStatusStore broken = Substitute.For<ICommandStatusStore>();
        broken.WriteStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CommandStatusRecord>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<CommandStatusRecord>(2).Status == CommandStatus.Completed
                ? Task.FromException(new IOException("status unavailable")) : Task.CompletedTask);
        var original = CreateActor(state, broken, timer.Manager);
        (await original.Actor.ProcessCommandAsync(Command())).Accepted.ShouldBeTrue();
        TerminalAdvisoryIntent intent = (TerminalAdvisoryIntent)state.CommittedState[TerminalAdvisoryIntent.GetStateKey("command-1")];
        ((TerminalAdvisoryRepairIndex)state.CommittedState[TerminalAdvisoryRepairIndex.StateKey]).Entries.ShouldHaveSingleItem().ReminderArmedAt.ShouldBeNull();
        await timer.AdvanceAsync(original.Actor, TimeSpan.FromMinutes(1));
        timer.DeliveryCount.ShouldBe(0);
        timer.FailRegistration = false;
        var recreated = CreateActor(state, statuses, timer.Manager);
        await ActivateAsync(recreated.Actor);
        timer.RegisteredNames.ShouldHaveSingleItem();
        int saves = state.Trace.Count(operation => operation == "SaveState");
        if (acknowledgementAfterCommit) { state.FaultAfterCall("SaveState", saves + 1, new IOException("acknowledgement lost")); }
        else { state.FaultOnCall("SaveState", saves + 1, new IOException("acknowledgement unavailable")); }
        await timer.AdvanceAsync(recreated.Actor, TimeSpan.FromMinutes(1));
        if (!acknowledgementAfterCommit)
        {
            state.CommittedState[TerminalAdvisoryIntent.GetStateKey("command-1")].ShouldBe(intent);
            await timer.AdvanceAsync(CreateActor(state, statuses, timer.Manager).Actor, TimeSpan.FromMinutes(1));
        }
        (await statuses.ReadStatusAsync(intent.TenantId, intent.MessageId)).ShouldBe(intent.Status);
        statuses.GetStatusHistory(intent.TenantId, intent.MessageId).ShouldAllBe(status => status == intent.Status);
        state.CommittedState.ContainsKey(TerminalAdvisoryIntent.GetStateKey(intent.MessageId)).ShouldBeFalse();
        ((TerminalAdvisoryRepairIndex)state.CommittedState[TerminalAdvisoryRepairIndex.StateKey]).Entries.ShouldBeEmpty();
        recreated.Publisher.PublishCalls.ShouldBeEmpty();
        await recreated.Invoker.DidNotReceive().InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>());
    }

    [Fact]
    public async Task Advisory_only_repair_keeps_an_already_verified_long_range_without_reader_access()
    {
        var state = new FaultInjectingActorStateManager();
        var statuses = new InMemoryCommandStatusStore();
        var timer = new RegisteredReminderHarness();
        TerminalAdvisoryIntent intent = ReviewIntent("command-1", (long)int.MaxValue + 1, 2);
        await state.SeedCommittedStateAsync(new Dictionary<string, object>
        {
            [TerminalAdvisoryIntent.GetStateKey(intent.MessageId)] = intent,
            [TerminalAdvisoryRepairIndex.StateKey] = new TerminalAdvisoryRepairIndex([new(intent.MessageId)]),
            [IdempotencyChecker.GetRecordKey(intent.MessageId)] = ReviewTerminal(intent),
        });
        var context = CreateActor(state, statuses, timer.Manager);
        await ActivateAsync(context.Actor);
        await timer.AdvanceAsync(context.Actor, TimeSpan.FromMinutes(1));
        (await statuses.ReadStatusAsync(intent.TenantId, intent.MessageId)).ShouldBe(intent.Status);
        state.CommittedState.ContainsKey(TerminalAdvisoryIntent.GetStateKey(intent.MessageId)).ShouldBeFalse();
        state.Trace.ShouldNotContain(operation => operation.Contains(":events:", StringComparison.Ordinal));
        context.Publisher.PublishCalls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Huge_coherent_missing_range_refuses_without_count_sized_allocation_or_evidence_loss()
    {
        var state = new FaultInjectingActorStateManager();
        var statuses = new InMemoryCommandStatusStore();
        CommandEnvelope command = Command();
        var checkpoint = new PipelineState(command.CorrelationId, CommandStatus.EventsStored, command.CommandType,
            DateTimeOffset.UnixEpoch, int.MaxValue, null, MessageId: command.MessageId, CausationId: command.MessageId,
            StartSequence: 1, EndSequence: int.MaxValue);
        await state.SeedCommittedStateAsync(new Dictionary<string, object> { [ActorIdentity + ":pipeline:" + command.CorrelationId] = checkpoint });
        var context = CreateActor(state, statuses);
        (await context.Actor.ProcessCommandAsync(command)).ErrorMessage.ShouldBe("command_identity_conflict");
        state.CommittedState[ActorIdentity + ":pipeline:" + command.CorrelationId].ShouldBe(checkpoint);
        state.Trace.Count(operation => operation == "TryGetState:" + ActorIdentity + ":events:1").ShouldBe(1);
        state.Trace.ShouldNotContain("SaveState");
        context.Publisher.PublishCalls.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Legacy_drain_distinguishes_deterministic_identity_corruption_from_transient_reads(bool transient)
    {
        var state = new FaultInjectingActorStateManager();
        var statuses = new InMemoryCommandStatusStore();
        CommandEnvelope command = Command();
        var record = new UnpublishedEventsRecord(command.CorrelationId, 1, 1, 1, command.CommandType, false,
            DateTimeOffset.UnixEpoch, 0, null, command.MessageId);
        var idempotency = ReviewTerminal(ReviewIntent(command.MessageId)) with { Disposition = IdempotencyRecordDisposition.Recoverable };
        var seed = new Dictionary<string, object>
        {
            [UnpublishedEventsRecord.GetStateKey(command.MessageId)] = record,
            [IdempotencyChecker.GetRecordKey(command.MessageId)] = idempotency,
            [UnpublishedPublicationIndex.StateKey] = new UnpublishedPublicationIndex([new(command.MessageId, command.CorrelationId, DateTimeOffset.UnixEpoch)]),
            ["pending_command_count"] = 1,
            [ActorIdentity + ":events:1"] = new EventEnvelope(Guid.NewGuid().ToString(), "agg-001", "test-domain", "test-tenant", "test-domain",
                1, 0, DateTimeOffset.UnixEpoch, command.CorrelationId, command.MessageId, "system", "1.0.0", "Created", 1, "json", [1], null),
        };
        await state.SeedCommittedStateAsync(seed);
        state.FaultOnCall("TryGetState:" + IdempotencyChecker.GetRecordKey(command.MessageId), 1,
            transient ? new IOException("private-provider-secret") : new JsonException("private-payload-secret"));
        var context = CreateActor(state, statuses);
        if (transient)
        {
            await Should.ThrowAsync<DrainStateStoreException>(() => context.Actor.ReceiveReminderAsync(
                UnpublishedEventsRecord.GetReminderName(command.MessageId), [], TimeSpan.Zero, TimeSpan.Zero));
        }
        else
        {
            await context.Actor.ReceiveReminderAsync(UnpublishedEventsRecord.GetReminderName(command.MessageId), [], TimeSpan.Zero, TimeSpan.Zero);
        }
        foreach ((string key, object value) in seed) { state.CommittedState[key].ShouldBe(value); }
        state.Trace.ShouldNotContain("SaveState");
        context.Publisher.PublishCalls.ShouldBeEmpty();
        await context.Actor.ReceiveReminderAsync(UnpublishedEventsRecord.GetReminderName(command.MessageId), [], TimeSpan.Zero, TimeSpan.Zero);
        context.Publisher.PublishCalls.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Duplicate_persisted_event_message_identity_refuses_then_exact_restore_resumes()
    {
        var state = new FaultInjectingActorStateManager();
        var statuses = new InMemoryCommandStatusStore();
        CommandEnvelope command = Command();
        var checkpoint = new PipelineState(command.CorrelationId, CommandStatus.EventsStored, command.CommandType,
            DateTimeOffset.UnixEpoch, 2, null, MessageId: command.MessageId, CausationId: command.MessageId, StartSequence: 1, EndSequence: 2);
        var first = new EventEnvelope(Guid.NewGuid().ToString(), "agg-001", "test-domain", "test-tenant", "test-domain", 1, 0,
            DateTimeOffset.UnixEpoch, command.CorrelationId, command.MessageId, "system", "1.0.0", "Created", 1, "json", [1], null);
        var second = first with { SequenceNumber = 2 };
        await state.SeedCommittedStateAsync(new Dictionary<string, object>
        {
            [ActorIdentity + ":pipeline:" + command.CorrelationId] = checkpoint,
            [ActorIdentity + ":events:1"] = first, [ActorIdentity + ":events:2"] = second,
        });
        var context = CreateActor(state, statuses);
        (await context.Actor.ProcessCommandAsync(command)).ErrorMessage.ShouldBe("command_identity_conflict");
        state.CommittedState[ActorIdentity + ":events:2"].ShouldBe(second);
        state.Trace.ShouldNotContain("SaveState");
        context.Publisher.PublishCalls.ShouldBeEmpty();
        var restored = new Dictionary<string, object>(state.CommittedState) { [ActorIdentity + ":events:2"] = second with { MessageId = Guid.NewGuid().ToString() } };
        await state.SeedCommittedStateAsync(restored);
        (await CreateActor(state, statuses).Actor.ProcessCommandAsync(command)).Accepted.ShouldBeTrue();
    }

    [Fact]
    public async Task Publication_owner_without_checkpoint_or_idempotency_refuses_fresh_execution_and_preserves_registered_drain()
    {
        var state = new FaultInjectingActorStateManager();
        var statuses = new InMemoryCommandStatusStore();
        var timer = new RegisteredReminderHarness();
        CommandEnvelope command = Command();
        var record = new UnpublishedEventsRecord(command.CorrelationId, 1, 1, 1, command.CommandType, false,
            DateTimeOffset.UnixEpoch, 0, null, command.MessageId, CausationId: command.MessageId);
        var seed = new Dictionary<string, object>
        {
            [UnpublishedEventsRecord.GetStateKey(command.MessageId)] = record,
            [UnpublishedPublicationIndex.StateKey] = new UnpublishedPublicationIndex([new(command.MessageId, command.CorrelationId, DateTimeOffset.UnixEpoch)]),
            ["pending_command_count"] = 1,
            [ActorIdentity + ":events:1"] = new EventEnvelope(Guid.NewGuid().ToString(), "agg-001", "test-domain", "test-tenant", "test-domain",
                1, 0, DateTimeOffset.UnixEpoch, command.CorrelationId, command.MessageId, "system", "1.0.0", "Created", 1, "json", [1], null),
        };
        await state.SeedCommittedStateAsync(seed);
        var context = CreateActor(state, statuses, timer.Manager);
        (await context.Actor.ProcessCommandAsync(command)).ErrorMessage.ShouldBe("command_identity_conflict");
        foreach ((string key, object value) in seed) { state.CommittedState[key].ShouldBe(value); }
        state.Trace.ShouldNotContain("SaveState");
        context.Publisher.PublishCalls.ShouldBeEmpty();
        await context.Invoker.DidNotReceive().InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>());
        await ActivateAsync(context.Actor);
        await timer.AdvanceAsync(context.Actor, TimeSpan.FromMinutes(1));
        context.Publisher.PublishCalls.ShouldHaveSingleItem().Events.ShouldHaveSingleItem().SequenceNumber.ShouldBe(1);
        state.CommittedState.ContainsKey(UnpublishedEventsRecord.GetStateKey(command.MessageId)).ShouldBeFalse();
        ((UnpublishedPublicationIndex)state.CommittedState[UnpublishedPublicationIndex.StateKey]).Entries.ShouldBeEmpty();
        await context.Invoker.DidNotReceive().InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Scan_save_ambiguity_recognizes_the_actual_raw_cursor_after_index_size_changes(bool committed)
    {
        var state = new FaultInjectingActorStateManager();
        var statuses = new InMemoryCommandStatusStore();
        var timer = new RegisteredReminderHarness();
        var seed = new Dictionary<string, object>();
        var entries = new List<TerminalAdvisoryRepairEntry>();
        for (int index = 0; index <= 32; index++)
        {
            string messageId = "pending-" + index;
            entries.Add(new(messageId));
            seed[TerminalAdvisoryIntent.GetStateKey(messageId)] = index == 32 ? ReviewIntent(messageId)
                : ReviewIntent(messageId) with { EventCount = -1 };
        }
        seed[TerminalAdvisoryRepairIndex.StateKey] = new TerminalAdvisoryRepairIndex(entries);
        // The original larger owner set allowed offset66; the current33-entry set normalizes it to0.
        seed[RecoveryScanProgress.AdvisoryKey] = new RecoveryScanProgress(66);
        await state.SeedCommittedStateAsync(seed);
        if (committed) { state.FaultAfterCall("SaveState", 1, new IOException("cursor acknowledgement lost")); }
        else { state.FaultOnCall("SaveState", 1, new IOException("cursor save unavailable")); }
        await ActivateAsync(CreateActor(state, statuses, timer.Manager).Actor);
        ((RecoveryScanProgress)state.CommittedState[RecoveryScanProgress.AdvisoryKey]).NextOffset.ShouldBe(committed ? 32 : 66);
        timer.RegisteredNames.ShouldBeEmpty();
        await ActivateAsync(CreateActor(state, statuses, timer.Manager).Actor);
        if (!committed)
        {
            ((RecoveryScanProgress)state.CommittedState[RecoveryScanProgress.AdvisoryKey]).NextOffset.ShouldBe(32);
            await ActivateAsync(CreateActor(state, statuses, timer.Manager).Actor);
        }
        timer.RegisteredNames.ShouldHaveSingleItem().ShouldBe(TerminalAdvisoryIntent.GetReminderName("pending-32"));
        for (int index = 0; index < 32; index++)
        {
            string key = TerminalAdvisoryIntent.GetStateKey("pending-" + index);
            state.CommittedState[key].ShouldBe(seed[key]);
        }
    }

    [Fact]
    public async Task Per_owner_failure_diagnostics_are_bounded_and_exclude_provider_details()
    {
        var state = new FaultInjectingActorStateManager();
        var statuses = new InMemoryCommandStatusStore();
        var timer = new RegisteredReminderHarness();
        // Capture the generated logger's pooled state during Log, before it is cleared.
        var logger = new SnapshotCaptureLogger();
        string failedOwner = "owner-" + new string('x', 250);
        await state.SeedCommittedStateAsync(new Dictionary<string, object>
        {
            [TerminalAdvisoryRepairIndex.StateKey] = new TerminalAdvisoryRepairIndex([new(failedOwner), new("healthy")]),
            [TerminalAdvisoryIntent.GetStateKey("healthy")] = ReviewIntent("healthy"),
        });
        state.FaultOnCall("TryGetState:" + TerminalAdvisoryIntent.GetStateKey(failedOwner), 1, new IOException("raw-private-provider-secret"));
        await ActivateAsync(CreateActor(state, statuses, timer.Manager, logger).Actor);
        timer.RegisteredNames.ShouldHaveSingleItem().ShouldBe(TerminalAdvisoryIntent.GetReminderName("healthy"));
        int[] diagnostics = Enumerable.Range(0, logger.EventIds.Count)
            .Where(index => logger.EventIds[index].Id == 8030).ToArray();
        diagnostics.ShouldNotBeEmpty();
        foreach (int diagnostic in diagnostics)
        {
            logger.Exceptions[diagnostic].ShouldBeNull();
            string rendered = logger.Messages[diagnostic];
            rendered.ShouldNotContain("raw-private-provider-secret");
            rendered.ShouldNotContain(failedOwner);
            rendered.ShouldContain("Reason=advisory_owner_read_or_rearm_failed");
        }

    }

    [Theory]
    [InlineData("infrastructure", false)]
    [InlineData("infrastructure", true)]
    [InlineData("backpressure", false)]
    [InlineData("backpressure", true)]
    [InlineData("publication-rejection", false)]
    [InlineData("publication-rejection", true)]
    [InlineData("domain-rejection", false)]
    [InlineData("domain-rejection", true)]
    public async Task Rejected_advisory_requires_domain_evidence_on_duplicate_and_registered_reminder(string classification, bool reminder)
    {
        var state = new FaultInjectingActorStateManager();
        var statuses = new InMemoryCommandStatusStore();
        var timer = new RegisteredReminderHarness();
        TerminalAdvisoryIntent intent = ReviewIntent("command-1") with
        {
            EventCount = 0, StartSequence = null, EndSequence = null,
            Status = ReviewIntent("command-1").Status with { Status = CommandStatus.Rejected, EventCount = 0, CommittedEventSequence = null },
        };
        IdempotencyRecord cached = ReviewTerminal(intent) with { Accepted = false, FailureReason = "DomainRejected" };
        await state.SeedCommittedStateAsync(new Dictionary<string, object>
        {
            [TerminalAdvisoryIntent.GetStateKey(intent.MessageId)] = intent,
            [TerminalAdvisoryRepairIndex.StateKey] = new TerminalAdvisoryRepairIndex([new(intent.MessageId)]),
            [IdempotencyChecker.GetRecordKey(intent.MessageId)] = cached,
        });
        var context = CreateActor(state, statuses, timer.Manager);
        await ActivateAsync(context.Actor);
        timer.RegisteredNames.ShouldHaveSingleItem();
        cached = classification switch
        {
            "infrastructure" => cached with { FailureReason = "DomainServiceUnavailable" },
            "backpressure" => cached with { BackpressureExceeded = true, BackpressurePendingCount = 5,
                BackpressureThreshold = 4, FailureReason = "BackpressureExceeded" },
            "publication-rejection" => cached with { RejectionEventType = "DomainRejectedEvent", FailureReason = "drain_publish_failed" },
            _ => cached,
        };
        var observed = new Dictionary<string, object>(state.CommittedState)
        {
            [IdempotencyChecker.GetRecordKey(intent.MessageId)] = cached,
        };
        await state.SeedCommittedStateAsync(observed);
        bool domainRejection = classification is "domain-rejection" or "publication-rejection";
        for (int retry = 0; retry < (domainRejection ? 1 : 3); retry++)
        {
            if (reminder) { await timer.AdvanceAsync(context.Actor, TimeSpan.FromMinutes(1)); }
            else { (await context.Actor.ProcessCommandAsync(Command())).ShouldBe(cached.ToResult()); }
        }
        if (domainRejection)
        {
            (await statuses.ReadStatusAsync(intent.TenantId, intent.MessageId)).ShouldBe(intent.Status);
            state.CommittedState.ContainsKey(TerminalAdvisoryIntent.GetStateKey(intent.MessageId)).ShouldBeFalse();
            ((TerminalAdvisoryRepairIndex)state.CommittedState[TerminalAdvisoryRepairIndex.StateKey]).Entries.ShouldBeEmpty();
        }
        else
        {
            statuses.GetStatusHistory(intent.TenantId, intent.MessageId).ShouldBeEmpty();
            foreach ((string key, object value) in observed) { state.CommittedState[key].ShouldBe(value); }
            state.Trace.ShouldNotContain("SaveState");
        }
        context.Publisher.PublishCalls.ShouldBeEmpty();
        await context.Invoker.DidNotReceive().InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>());
    }

    [Theory]
    [InlineData("rejection")]
    [InlineData("success")]
    [InlineData("infrastructure")]
    [InlineData("backpressure")]
    public async Task Legacy_rejection_drain_matches_domain_disposition_and_retains_contradictory_evidence(string classification)
    {
        var state = new FaultInjectingActorStateManager();
        var statuses = new InMemoryCommandStatusStore();
        var timer = new RegisteredReminderHarness();
        CommandEnvelope command = Command();
        var record = new UnpublishedEventsRecord(command.CorrelationId, 1, 1, 1, command.CommandType, true,
            DateTimeOffset.UnixEpoch, 0, null, command.MessageId);
        var cached = ReviewTerminal(ReviewIntent(command.MessageId)) with
        {
            Accepted = false, RejectionEventType = "DomainRejectedEvent", FailureReason = "drain_publish_failed",
            Disposition = IdempotencyRecordDisposition.Recoverable,
        };
        await state.SeedCommittedStateAsync(new Dictionary<string, object>
        {
            [UnpublishedEventsRecord.GetStateKey(command.MessageId)] = record,
            [IdempotencyChecker.GetRecordKey(command.MessageId)] = cached,
            [UnpublishedPublicationIndex.StateKey] = new UnpublishedPublicationIndex([new(command.MessageId, command.CorrelationId, DateTimeOffset.UnixEpoch)]),
            ["pending_command_count"] = 1,
            [ActorIdentity + ":events:1"] = new EventEnvelope(Guid.NewGuid().ToString(), "agg-001", "test-domain", "test-tenant", "test-domain",
                1, 0, DateTimeOffset.UnixEpoch, command.CorrelationId, command.MessageId, "system", "1.0.0", "DomainRejectedEvent", 1, "json", [1], null),
            ["unrelated-aggregate-evidence"] = "unchanged",
        });
        ICommandStatusStore broken = Substitute.For<ICommandStatusStore>();
        broken.WriteStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CommandStatusRecord>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("status unavailable")));
        var original = CreateActor(state, broken, timer.Manager);
        await ActivateAsync(original.Actor);
        timer.RegisteredNames.ShouldHaveSingleItem().ShouldBe(UnpublishedEventsRecord.GetReminderName(command.MessageId));
        cached = classification switch
        {
            "success" => cached with { Accepted = true, RejectionEventType = null },
            "infrastructure" => cached with { RejectionEventType = null, FailureReason = "DomainServiceUnavailable" },
            "backpressure" => cached with { BackpressureExceeded = true, FailureReason = "BackpressureExceeded" },
            _ => cached,
        };
        var observed = new Dictionary<string, object>(state.CommittedState)
        {
            [IdempotencyChecker.GetRecordKey(command.MessageId)] = cached,
        };
        await state.SeedCommittedStateAsync(observed);
        if (classification != "rejection")
        {
            for (int retry = 0; retry < 3; retry++) { await timer.AdvanceAsync(original.Actor, TimeSpan.FromMinutes(1)); }
            original.Publisher.PublishCalls.ShouldBeEmpty();
            foreach ((string key, object value) in observed) { state.CommittedState[key].ShouldBe(value); }
            state.Trace.ShouldNotContain("SaveState");
        }
        else
        {
            await timer.AdvanceAsync(original.Actor, TimeSpan.FromMinutes(1));
            original.Publisher.PublishCalls.ShouldHaveSingleItem().Events.ShouldHaveSingleItem().SequenceNumber.ShouldBe(1);
            state.CommittedState.ContainsKey(UnpublishedEventsRecord.GetStateKey(command.MessageId)).ShouldBeFalse();
            var intent = (TerminalAdvisoryIntent)state.CommittedState[TerminalAdvisoryIntent.GetStateKey(command.MessageId)];
            intent.Status.Status.ShouldBe(CommandStatus.Rejected);
            intent.Status.CommittedEventSequence.ShouldBeNull();
            var recreated = CreateActor(state, statuses, timer.Manager);
            await timer.AdvanceAsync(recreated.Actor, TimeSpan.FromMinutes(1));
            (await statuses.ReadStatusAsync(command.TenantId, command.MessageId)).ShouldBe(intent.Status);
            state.CommittedState.ContainsKey(TerminalAdvisoryIntent.GetStateKey(command.MessageId)).ShouldBeFalse();
            ((UnpublishedPublicationIndex)state.CommittedState[UnpublishedPublicationIndex.StateKey]).Entries.ShouldBeEmpty();
            ((TerminalAdvisoryRepairIndex)state.CommittedState[TerminalAdvisoryRepairIndex.StateKey]).Entries.ShouldBeEmpty();
            ((IdempotencyRecord)state.CommittedState[IdempotencyChecker.GetRecordKey(command.MessageId)]).ToResult().ShouldBe(cached.ToResult());
            recreated.Publisher.PublishCalls.ShouldBeEmpty();
            await recreated.Invoker.DidNotReceive().InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>());
        }
        state.CommittedState["unrelated-aggregate-evidence"].ShouldBe("unchanged");
        await original.Invoker.DidNotReceive().InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>());
    }
}
