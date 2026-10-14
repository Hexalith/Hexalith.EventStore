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
        CreateActor(IActorStateManager state, ICommandStatusStore statuses)
    {
        var timers = Substitute.For<ActorTimerManager>();
        var host = ActorHost.CreateForTest<AggregateActor>(new ActorTestOptions
        {
            ActorId = new ActorId(ActorIdentity), TimerManager = timers,
        });
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .Returns(DomainResult.Success([new CommandRecoveryEvent(), new CommandRecoveryEvent()]));
        var publisher = new FakeEventPublisher();
        var actor = new AggregateActor(host, Substitute.For<ILogger<AggregateActor>>(), invoker,
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
}
