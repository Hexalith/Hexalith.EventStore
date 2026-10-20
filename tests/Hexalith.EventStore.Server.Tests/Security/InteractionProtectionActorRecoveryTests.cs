using System.Text.Json;
using System.Reflection;

using Dapr.Actors.Runtime;
using Dapr.Client;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Snapshots;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.PayloadProtection.Tests;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Tests.Actors;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;
using Shouldly;

using static Hexalith.EventStore.Server.Tests.Actors.AggregateActorTestHelper;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Actual engine/actor coordination tests with independently inspected in-memory committed source state.</summary>
public sealed class InteractionProtectionActorRecoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimedOutWriterProofQuiescesBeforeCommandReentryAndRuntimeCleanup(bool failedMethod)
    {
        var fixture = Fixture();
        var state = new FaultInjectingActorStateManager();
        var scheduler = new RegisteredReminderHarness();
        var clock = new FakeTimeProvider();
        ConfigureWriterProof(fixture, state, static () => false);
        ActorTestContext actor = Actor(fixture, state, scheduler: scheduler.Manager, clock: clock);
        CommandEnvelope command = Command(fixture, "unfinished-writer");
        await Should.ThrowAsync<PayloadProtectionRecoveryException>(() => actor.Actor.ProcessCommandAsync(command));
        var original = state.CreateCommittedView()[PayloadProtectionCompletionJournal.StateName].ShouldBeOfType<PayloadProtectionCompletionJournal>();
        byte[] ciphertext = fixture.Occurrences.Record!.Sealed!.PayloadBytes.ToArray();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int completions = 0;
        fixture.Occurrences.Registry.CompleteWriterAsync(Arg.Any<InteractionOccurrenceIdentity>(), Arg.Any<string>(),
            Arg.Any<string>(), true, Arg.Any<CancellationToken>()).Returns(async call =>
        {
            entered.TrySetResult();
            await release.Task; // An independently committing writer may not acknowledge cancellation.
            completions++;
            fixture.Occurrences.Record = fixture.Occurrences.Record! with
            {
                WriterState = InteractionOccurrenceWriterState.Active,
                WriterProofId = call.ArgAt<string>(2),
            };
            return new InteractionOccurrenceReservationResult(InteractionOccurrenceReservationStatus.Sealed, fixture.Occurrences.Record);
        });

        Task delivery = scheduler.AdvanceAsync(actor.Actor, TimeSpan.FromSeconds(5));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(30));
        await Should.ThrowAsync<PayloadProtectionRecoveryException>(() => delivery.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        int operationsBefore = state.Trace.Count;
        int proofsBefore = fixture.Occurrences.Registry.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IInteractionOccurrenceRegistry.CompleteWriterAsync));
        try
        {
            await Should.ThrowAsync<PayloadProtectionRecoveryException>(() => actor.Actor.ProcessCommandAsync(command)
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            await Should.ThrowAsync<PayloadProtectionRecoveryException>(() => actor.Actor.ReceiveReminderAsync(
                PayloadProtectionCompletionJournal.ReminderName, [], TimeSpan.Zero, TimeSpan.Zero));
            state.Trace.Count.ShouldBe(operationsBefore);
            fixture.Occurrences.Registry.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IInteractionOccurrenceRegistry.CompleteWriterAsync))
                .ShouldBe(proofsBefore);
            state.CreateCommittedView()[PayloadProtectionCompletionJournal.StateName].ShouldBe(original);
            completions.ShouldBe(0);

            MethodInfo method = typeof(Dapr.Actors.Runtime.Actor).GetMethod(failedMethod ? "OnActorMethodFailedInternalAsync" : "OnPostActorMethodAsyncInternal",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var cleanup = (Task)method.Invoke(actor.Actor, failedMethod
                ? [default(ActorMethodContext), new OperationCanceledException()]
                : [default(ActorMethodContext)])!;
            cleanup.IsCompleted.ShouldBeFalse();
            state.Trace.Count.ShouldBe(operationsBefore);
            release.SetResult();
            if (failedMethod) { await cleanup.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); }
            else
            {
                await Should.ThrowAsync<OperationCanceledException>(() => cleanup.WaitAsync(
                    TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            }
            completions.ShouldBe(1);
            scheduler.RegisteredNames.ShouldContain(PayloadProtectionCompletionJournal.ReminderName);
            await scheduler.AdvanceAsync(actor.Actor, TimeSpan.FromSeconds(30));

            var completed = state.CreateCommittedView()[PayloadProtectionCompletionJournal.StateName].ShouldBeOfType<PayloadProtectionCompletionJournal>();
            completed.Completed.ShouldBeTrue();
            completed.Originals.ShouldBe(original.Originals);
            fixture.Occurrences.Record!.Sealed!.PayloadBytes.ShouldBe(ciphertext);
            fixture.Occurrences.Keys.Leases.ShouldBe(1);
            scheduler.RegisteredNames.ShouldBeEmpty();
            await actor.EventPublisher.DidNotReceiveWithAnyArgs().PublishEventsAsync(default!, default!, default!, default, default);
        }
        finally { release.TrySetResult(); }
    }

    [Fact]
    public async Task IdleReadAndWriterProofShareOneElapsedDeadline()
    {
        var fixture = Fixture();
        var state = new FaultInjectingActorStateManager();
        var scheduler = new RegisteredReminderHarness();
        var clock = new FakeTimeProvider();
        ConfigureWriterProof(fixture, state, static () => false);
        ActorTestContext actor = Actor(fixture, state, scheduler: scheduler.Manager, clock: clock);
        await Should.ThrowAsync<PayloadProtectionRecoveryException>(() => actor.Actor.ProcessCommandAsync(Command(fixture, "deadline")));
        var original = state.CreateCommittedView()[PayloadProtectionCompletionJournal.StateName];
        var readEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var proofEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string read = $"TryGetState:{PayloadProtectionCompletionJournal.StateName}";
        state.ActBeforeCall(read, state.Trace.Count(operation => operation == read) + 1, async _ =>
        {
            Task delay = Task.Delay(TimeSpan.FromSeconds(25), clock, TestContext.Current.CancellationToken);
            readEntered.SetResult();
            await delay;
        });
        fixture.Occurrences.Registry.CompleteWriterAsync(Arg.Any<InteractionOccurrenceIdentity>(), Arg.Any<string>(),
            Arg.Any<string>(), true, Arg.Any<CancellationToken>()).Returns(async call =>
        {
            Task delay = Task.Delay(TimeSpan.FromSeconds(10), clock, call.ArgAt<CancellationToken>(4));
            proofEntered.SetResult();
            await delay;
            return new InteractionOccurrenceReservationResult(InteractionOccurrenceReservationStatus.Sealed,
                fixture.Occurrences.Record! with { WriterState = InteractionOccurrenceWriterState.Active });
        });

        Task delivery = scheduler.AdvanceAsync(actor.Actor, TimeSpan.FromSeconds(5));
        await readEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(25));
        await proofEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(5));
        await Should.ThrowAsync<PayloadProtectionRecoveryException>(() => delivery.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        state.CreateCommittedView()[PayloadProtectionCompletionJournal.StateName].ShouldBe(original);
        scheduler.RegisteredNames.ShouldContain(PayloadProtectionCompletionJournal.ReminderName);
        fixture.Occurrences.Keys.Leases.ShouldBe(1);
    }

    [Theory]
    [InlineData("barrier")]
    [InlineData("save")]
    public async Task TimedOutStateIoQuiescesBeforeLaterTurnsAndRuntimeCleanup(string operation)
    {
        var fixture = Fixture();
        var state = new FaultInjectingActorStateManager();
        var scheduler = new RegisteredReminderHarness();
        var clock = new FakeTimeProvider();
        bool available = false;
        ConfigureWriterProof(fixture, state, () => available);
        ActorTestContext actor = Actor(fixture, state, scheduler: scheduler.Manager, clock: clock);
        await Should.ThrowAsync<PayloadProtectionRecoveryException>(() => actor.Actor.ProcessCommandAsync(Command(fixture, "unfinished-io")));
        var original = state.CreateCommittedView()[PayloadProtectionCompletionJournal.StateName].ShouldBeOfType<PayloadProtectionCompletionJournal>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string stateOperation = operation == "barrier" ? "ClearCache" : "SaveState";
        if (operation == "barrier")
        {
            typeof(AggregateActor).GetField("_stateCacheUnsafe", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(actor.Actor, true);
        }
        state.ActBeforeCall(stateOperation, state.Trace.Count(value => value == stateOperation) + 1, async _ =>
        {
            entered.SetResult();
            await release.Task; // Deliberately ignores cancellation, as an unknown store operation may do.
        });
        available = true;

        Task delivery = scheduler.AdvanceAsync(actor.Actor, TimeSpan.FromSeconds(5));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(30));
        await Should.ThrowAsync<OperationCanceledException>(() => delivery.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        int operationsBefore = state.Trace.Count;
        await Should.ThrowAsync<PayloadProtectionRecoveryException>(() => actor.Actor.ReceiveReminderAsync(
            PayloadProtectionCompletionJournal.ReminderName, [], TimeSpan.Zero, TimeSpan.Zero));
        state.Trace.Count.ShouldBe(operationsBefore);
        scheduler.RegisteredNames.ShouldContain(PayloadProtectionCompletionJournal.ReminderName);

        var failedMethod = typeof(Dapr.Actors.Runtime.Actor).GetMethod("OnActorMethodFailedInternalAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var cleanup = (Task)failedMethod.Invoke(actor.Actor, [default(ActorMethodContext), new OperationCanceledException()])!;
        cleanup.IsCompleted.ShouldBeFalse();
        state.Trace.Count.ShouldBe(operationsBefore);
        release.SetResult();
        await cleanup.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await scheduler.AdvanceAsync(actor.Actor, TimeSpan.FromSeconds(30));

        var completed = state.CreateCommittedView()[PayloadProtectionCompletionJournal.StateName].ShouldBeOfType<PayloadProtectionCompletionJournal>();
        completed.Completed.ShouldBeTrue();
        completed.Originals.ShouldBe(original.Originals);
        scheduler.RegisteredNames.ShouldBeEmpty();
        fixture.Occurrences.Keys.Leases.ShouldBe(1);
    }

    [Fact]
    public async Task SchedulerRegistrationDeadlineRetainsUnknownAcknowledgementUntilQuiescent()
    {
        var fixture = Fixture();
        var state = new FaultInjectingActorStateManager();
        var manager = Substitute.For<ActorTimerManager>();
        var clock = new FakeTimeProvider();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int acknowledgements = 0;
        manager.RegisterReminderAsync(Arg.Any<ActorReminder>()).Returns(async _ =>
        {
            entered.SetResult();
            await release.Task;
            acknowledgements++;
        });
        ConfigureWriterProof(fixture, state, static () => true);
        ActorTestContext actor = Actor(fixture, state, scheduler: manager, clock: clock);

        Task command = actor.Actor.ProcessCommandAsync(Command(fixture, "registration-deadline"));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(30));
        await Should.ThrowAsync<OperationCanceledException>(() => command.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        state.CreateCommittedView().Keys.ShouldNotContain($"{fixture.Binding.Identity.Owner.EventStreamKeyPrefix}1");
        state.CreateCommittedView().Keys.ShouldNotContain(PayloadProtectionCompletionJournal.StateName);
        acknowledgements.ShouldBe(0);
        var failedMethod = typeof(Dapr.Actors.Runtime.Actor).GetMethod("OnActorMethodFailedInternalAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var cleanup = (Task)failedMethod.Invoke(actor.Actor, [default(ActorMethodContext), new OperationCanceledException()])!;
        cleanup.IsCompleted.ShouldBeFalse();
        release.SetResult();
        await cleanup.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        acknowledgements.ShouldBe(1);
        state.CreateCommittedView().Keys.ShouldNotContain($"{fixture.Binding.Identity.Owner.EventStreamKeyPrefix}1");
        fixture.Occurrences.Record!.WriterState.ShouldBe(InteractionOccurrenceWriterState.SealedPending);
        fixture.Occurrences.Keys.Leases.ShouldBe(1);
    }

    [Fact]
    public async Task RegisteredReminderCompletesIdleOriginalWithoutAnotherCommandOrEncryption()
    {
        var fixture = Fixture();
        var state = new FaultInjectingActorStateManager();
        var scheduler = new RegisteredReminderHarness();
        bool available = false;
        ConfigureWriterProof(fixture, state, () => available);
        ActorTestContext actor = Actor(fixture, state, scheduler: scheduler.Manager);
        await Should.ThrowAsync<PayloadProtectionRecoveryException>(() => actor.Actor.ProcessCommandAsync(Command(fixture, "idle")));
        scheduler.RegisteredNames.ShouldContain(PayloadProtectionCompletionJournal.ReminderName);
        var original = state.CommittedState[PayloadProtectionCompletionJournal.StateName].ShouldBeOfType<PayloadProtectionCompletionJournal>();
        byte[] ciphertext = fixture.Occurrences.Record!.Sealed!.PayloadBytes.ToArray();
        available = true;

        await scheduler.AdvanceAsync(actor.Actor, TimeSpan.FromSeconds(5));

        scheduler.DeliveryCount.ShouldBe(1);
        scheduler.RegisteredNames.ShouldNotContain(PayloadProtectionCompletionJournal.ReminderName);
        var completed = state.CreateCommittedView()[PayloadProtectionCompletionJournal.StateName].ShouldBeOfType<PayloadProtectionCompletionJournal>();
        completed.Completed.ShouldBeTrue();
        completed.Originals.ShouldBe(original.Originals);
        fixture.Occurrences.Record.WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
        fixture.Occurrences.Record.Sealed!.PayloadBytes.ShouldBe(ciphertext);
        fixture.Occurrences.Keys.Leases.ShouldBe(1);
        actor.Invoker.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IDomainServiceInvoker.InvokeAsync)).ShouldBe(1);
        await actor.EventPublisher.DidNotReceiveWithAnyArgs().PublishEventsAsync(default!, default!, default!, default, default);
        await actor.Actor.ReceiveReminderAsync(PayloadProtectionCompletionJournal.ReminderName, [], TimeSpan.Zero, TimeSpan.Zero);
        state.CreateCommittedView()[PayloadProtectionCompletionJournal.StateName].ShouldBe(completed);
        fixture.Occurrences.Keys.Leases.ShouldBe(1);
    }

    [Fact]
    public async Task UnavailableWriterProofRetainsIdleReminderAndExactPendingOriginal()
    {
        var fixture = Fixture();
        var state = new FaultInjectingActorStateManager();
        var scheduler = new RegisteredReminderHarness();
        bool available = false;
        ConfigureWriterProof(fixture, state, () => available);
        ActorTestContext actor = Actor(fixture, state, scheduler: scheduler.Manager);
        await Should.ThrowAsync<PayloadProtectionRecoveryException>(() => actor.Actor.ProcessCommandAsync(Command(fixture, "outage")));
        var original = state.CreateCommittedView()[PayloadProtectionCompletionJournal.StateName].ShouldBeOfType<PayloadProtectionCompletionJournal>();

        await Should.ThrowAsync<PayloadProtectionRecoveryException>(() => scheduler.AdvanceAsync(actor.Actor, TimeSpan.FromSeconds(5)));
        state.CreateCommittedView()[PayloadProtectionCompletionJournal.StateName].ShouldBe(original);
        scheduler.RegisteredNames.ShouldContain(PayloadProtectionCompletionJournal.ReminderName);
        fixture.Occurrences.Record!.WriterState.ShouldBe(InteractionOccurrenceWriterState.SealedPending);
        available = true;
        await scheduler.AdvanceAsync(actor.Actor, TimeSpan.FromSeconds(30));
        state.CreateCommittedView()[PayloadProtectionCompletionJournal.StateName].ShouldBeOfType<PayloadProtectionCompletionJournal>().Completed.ShouldBeTrue();
        scheduler.RegisteredNames.ShouldBeEmpty();
        fixture.Occurrences.Keys.Leases.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IdleCompletionSaveFailureRecoversByDurableReadWithoutDroppingReminder(bool committed)
    {
        var fixture = Fixture();
        var state = new FaultInjectingActorStateManager();
        var scheduler = new RegisteredReminderHarness();
        bool available = false;
        ConfigureWriterProof(fixture, state, () => available);
        ActorTestContext actor = Actor(fixture, state, scheduler: scheduler.Manager);
        await Should.ThrowAsync<PayloadProtectionRecoveryException>(() => actor.Actor.ProcessCommandAsync(Command(fixture, "save-loss")));
        int nextSave = state.Trace.Count(operation => operation == "SaveState") + 1;
        if (committed) { state.FaultAfterCall("SaveState", nextSave, new IOException("lost completion acknowledgement")); }
        else { state.FaultOnCall("SaveState", nextSave, new IOException("completion save unavailable")); }
        available = true;

        await Should.ThrowAsync<IOException>(() => scheduler.AdvanceAsync(actor.Actor, TimeSpan.FromSeconds(5)));
        state.CreateCommittedView()[PayloadProtectionCompletionJournal.StateName].ShouldBeOfType<PayloadProtectionCompletionJournal>().Completed.ShouldBe(committed);
        scheduler.RegisteredNames.ShouldContain(PayloadProtectionCompletionJournal.ReminderName);
        await scheduler.AdvanceAsync(actor.Actor, TimeSpan.FromSeconds(30));
        state.CreateCommittedView()[PayloadProtectionCompletionJournal.StateName].ShouldBeOfType<PayloadProtectionCompletionJournal>().Completed.ShouldBeTrue();
        scheduler.RegisteredNames.ShouldBeEmpty();
        fixture.Occurrences.Keys.Leases.ShouldBe(1);
    }

    [Fact]
    public async Task ActivationRearmsExistingPendingJournalWithoutCompletingOrReservingAnOccurrence()
    {
        var fixture = Fixture();
        var state = new FaultInjectingActorStateManager();
        ConfigureWriterProof(fixture, state, static () => false);
        await Should.ThrowAsync<PayloadProtectionRecoveryException>(() => Actor(fixture, state).Actor.ProcessCommandAsync(Command(fixture, "reactivation")));
        // Model an idle actor whose only recovery work is the original protection
        // journal. A publication index would independently run command recovery.
        var idleState = new FaultInjectingActorStateManager();
        await idleState.SeedCommittedStateAsync(state.CreateCommittedView()
            .Where(entry => entry.Key == PayloadProtectionCompletionJournal.StateName
                || entry.Key.StartsWith(fixture.Binding.Identity.Owner.EventStreamKeyPrefix, StringComparison.Ordinal))
            .ToDictionary(entry => entry.Key, entry => entry.Value));
        var scheduler = new RegisteredReminderHarness();
        ActorTestContext restarted = Actor(fixture, idleState, scheduler: scheduler.Manager);
        fixture.Occurrences.Registry.ClearReceivedCalls();
        var activate = typeof(AggregateActor).GetMethod("OnActivateAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        await (Task)activate.Invoke(restarted.Actor, null)!;

        scheduler.RegisteredNames.ShouldContain(PayloadProtectionCompletionJournal.ReminderName);
        fixture.Occurrences.Registry.ReceivedCalls().Select(call => call.GetMethodInfo().Name)
            .Where(name => name != nameof(IInteractionOccurrenceRegistry.LookupAsync)).ShouldBeEmpty();
        idleState.CreateCommittedView()[PayloadProtectionCompletionJournal.StateName].ShouldBeOfType<PayloadProtectionCompletionJournal>().Completed.ShouldBeFalse();
    }

    [Fact]
    public async Task SchedulerFailurePreventsProtectedSourceCommit()
    {
        var fixture = Fixture();
        var state = new FaultInjectingActorStateManager();
        var scheduler = new RegisteredReminderHarness { FailRegistration = true };
        ConfigureWriterProof(fixture, state, static () => true);
        ActorTestContext actor = Actor(fixture, state, scheduler: scheduler.Manager);

        (await actor.Actor.ProcessCommandAsync(Command(fixture, "scheduler-outage"))).Accepted.ShouldBeFalse();

        state.CreateCommittedView().Keys.ShouldNotContain($"{fixture.Binding.Identity.Owner.EventStreamKeyPrefix}1");
        state.CreateCommittedView().Keys.ShouldNotContain(PayloadProtectionCompletionJournal.StateName);
        scheduler.RegisteredNames.ShouldBeEmpty();
        fixture.Occurrences.Record!.WriterState.ShouldBe(InteractionOccurrenceWriterState.SealedPending);
        await actor.EventPublisher.DidNotReceiveWithAnyArgs().PublishEventsAsync(default!, default!, default!, default, default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessCommandCommitsOriginalJournalAndCompletesBeforePublication(bool loseSaveAcknowledgement)
    {
        var fixture = Fixture();
        var state = new FaultInjectingActorStateManager();
        ConfigureWriterProof(fixture, state, static () => true);
        if (loseSaveAcknowledgement) { state.FaultAfterCall("SaveState", 2, new InvalidOperationException("lost acknowledgement")); }
        ActorTestContext actor = Actor(fixture, state);
        CommandEnvelope command = Command(fixture, "create");
        (await actor.Actor.ProcessCommandAsync(command)).Accepted.ShouldBeTrue();
        fixture.Occurrences.Record!.WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
        PayloadProtectionCompletionJournal journal = state.CommittedState[PayloadProtectionCompletionJournal.StateName]
            .ShouldBeOfType<PayloadProtectionCompletionJournal>();
        journal.Completed.ShouldBeTrue();
        journal.Originals.ShouldHaveSingleItem().OperationId.ShouldBe(fixture.Binding.OperationId);
        var stored = state.CommittedState[$"{command.AggregateIdentity.EventStreamKeyPrefix}1"].ShouldBeOfType<EventEnvelope>();
        stored.Payload.ShouldBe(fixture.Occurrences.Record.Sealed!.PayloadBytes);
        stored.Extensions!.Values.ShouldNotContain(fixture.Binding.OperationId);
        fixture.Occurrences.Keys.Leases.ShouldBe(1);
        await actor.EventPublisher.Received(1).PublishEventsAsync(command.AggregateIdentity,
            Arg.Is<IReadOnlyList<EventEnvelope>>(events => events.Count == 1), command.CorrelationId,
            Arg.Any<CancellationToken>(), false);
    }

    [Fact]
    public async Task CompletionInterruptionRecoversOriginalJournalOnEventsStoredResume()
    {
        var fixture = Fixture();
        var state = new FaultInjectingActorStateManager();
        bool available = false;
        ConfigureWriterProof(fixture, state, () => available);
        ActorTestContext actor = Actor(fixture, state);
        CommandEnvelope command = Command(fixture, "interrupted");
        await Should.ThrowAsync<PayloadProtectionRecoveryException>(() => actor.Actor.ProcessCommandAsync(command));
        var pending = state.CommittedState[PayloadProtectionCompletionJournal.StateName].ShouldBeOfType<PayloadProtectionCompletionJournal>();
        pending.Completed.ShouldBeFalse();
        state.CommittedState[$"{command.AggregateIdentity.PipelineKeyPrefix}{command.CorrelationId}"]
            .ShouldBeOfType<PipelineState>().CurrentStage.ShouldBe(CommandStatus.EventsStored);
        await actor.EventPublisher.DidNotReceiveWithAnyArgs().PublishEventsAsync(default!, default!, default!, default, default);
        available = true;
        await state.ClearCacheAsync();
        ActorTestContext resumed = Actor(fixture, state);
        (await resumed.Actor.ProcessCommandAsync(command)).Accepted.ShouldBeTrue();
        fixture.Occurrences.Record!.WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
        state.CommittedState[PayloadProtectionCompletionJournal.StateName].ShouldBeOfType<PayloadProtectionCompletionJournal>()
            .Originals.ShouldBe(pending.Originals);
        fixture.Occurrences.Keys.Leases.ShouldBe(1);
        resumed.Invoker.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task ConfirmedCommitWithLostCancellationResultCompletesUsingIndependentRecoveryBudget()
    {
        var fixture = Fixture();
        var state = new FaultInjectingActorStateManager();
        ConfigureWriterProof(fixture, state, static () => true);
        state.FaultAfterCall("SaveState", 2, new OperationCanceledException());
        ActorTestContext actor = Actor(fixture, state);
        await Should.ThrowAsync<OperationCanceledException>(() => actor.Actor.ProcessCommandAsync(Command(fixture, "lost-result")));
        fixture.Occurrences.Record!.WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
        state.CommittedState.ContainsKey(PayloadProtectionCompletionJournal.StateName).ShouldBeTrue();
        fixture.Occurrences.Keys.Leases.ShouldBe(1);
    }

    [Fact]
    public async Task ProductionActorFoldsErasedWhileExternalProductionReaderRemainsUnreadable()
    {
        var fixture = Fixture();
        var state = new FaultInjectingActorStateManager();
        ConfigureWriterProof(fixture, state, static () => true);
        using EventEvolutionManifestCandidate candidate = Candidate();
        var services = new ServiceCollection();
        services.AddKeyedSingleton("d", candidate);
        using ServiceProvider provider = services.BuildServiceProvider();
        ActorTestContext actor = Actor(fixture, state, provider);
        (await actor.Actor.ProcessCommandAsync(Command(fixture, "first"))).Accepted.ShouldBeTrue();
        fixture.Occurrences.Keys.RootResolver = _ => Task.FromResult<Hexalith.EventStore.PayloadProtection.InteractionOccurrenceOwnedKey?>(null);
        fixture.Erased = true;
        object? observed = null;
        actor.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(call => { observed = call.ArgAt<object?>(1); return DomainResult.NoOp(); });
        (await actor.Actor.ProcessCommandAsync(Command(fixture, "second"))).Accepted.ShouldBeTrue();
        var domain = observed.ShouldBeOfType<DomainServiceCurrentState>();
        using JsonDocument erased = JsonDocument.Parse(domain.Events.Single().Payload);
        erased.RootElement.GetProperty("Safe").GetInt32().ShouldBe(1);
        erased.RootElement.GetProperty("ProtectedContent").GetProperty("Value").GetProperty("State").GetString().ShouldBe(nameof(Erased));
        DaprProductionLogicalEventReader external = DaprProductionLogicalEventReader.FromCallerPin(state, fixture.Service, candidate)!;
        await Should.ThrowAsync<ProtectedDataUnreadableException>(() => external.ReadPageAsync(
            fixture.Binding.Identity.Owner, "r", 1, 1, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("changed")]
    [InlineData("lineage")]
    public async Task ActualEngineLogicalReaderRequiresOriginalCiphertextDigestBeforeProviderCalls(string mutation)
    {
        var fixture = Fixture();
        var state = new FaultInjectingActorStateManager();
        ConfigureWriterProof(fixture, state, static () => true);
        (await Actor(fixture, state).Actor.ProcessCommandAsync(Command(fixture, "first"))).Accepted.ShouldBeTrue();
        string key = $"{fixture.Binding.Identity.Owner.EventStreamKeyPrefix}1";
        var source = (EventEnvelope)state.CommittedState[key];
        EventEnvelope changed = mutation switch {
            "missing" => source with { ApplicationPayloadDigest = null },
            "changed" => source with { ApplicationPayloadDigest = new string('0', 64) },
            _ => source with { CorrelationId = "substituted" },
        };
        await state.SeedCommittedStateAsync(new Dictionary<string, object> { [key] = changed });
        using EventEvolutionManifestCandidate candidate = Candidate();
        DaprProductionLogicalEventReader reader = DaprProductionLogicalEventReader.FromCallerPin(state, fixture.Service, candidate)!;
        fixture.Occurrences.Registry.ClearReceivedCalls();
        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() => reader.ReadPageAsync(
            fixture.Binding.Identity.Owner, "r", 1, 1, TestContext.Current.CancellationToken));
        failure.Message.ShouldContain("LogicalDigestMismatch");
        fixture.Occurrences.Registry.ReceivedCalls().ShouldBeEmpty();
        source.Payload.ShouldBe(changed.Payload);
    }

    [Fact]
    public async Task ManualSnapshotLostSaveAcknowledgementCompletesExactOriginalBeforeCreated()
    {
        var fixture = Fixture();
        fixture.Binding = fixture.Binding with { Identity = fixture.Binding.Identity with {
            Kind = PayloadProtectionPayloadKind.Snapshot, PayloadTypeId = "hx-snapshot-v1:folded-state" } };
        var state = new FaultInjectingActorStateManager();
        AggregateIdentity owner = fixture.Binding.Identity.Owner;
        var source = new EventEnvelope(InteractionOccurrenceProtectorFixture.Reference, owner.AggregateId, "r", owner.TenantId, owner.Domain, 1, 1,
            DateTimeOffset.UnixEpoch, "source", "source", "actor", "v1", "Legacy.Event", 1, "json", "{}"u8.ToArray(), null);
        await state.SeedCommittedStateAsync(new Dictionary<string, object> {
            [owner.MetadataKey] = new AggregateMetadata(1, DateTimeOffset.UnixEpoch, null),
            [$"{owner.EventStreamKeyPrefix}1"] = source,
        });
        var reconstructor = Substitute.For<IAggregateStateReconstructor>();
        reconstructor.ReconstructAsync(owner, "r", Arg.Any<IReadOnlyList<EventEnvelope>>(), 1, false,
            Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(AggregateReconstructionResult.Succeeded("{\"count\":1}", 1));
        var services = new ServiceCollection();
        services.AddSingleton(reconstructor);
        services.AddSingleton(new FoldedSnapshotQualificationRegistry([new FoldedSnapshotQualification("d", "r", typeof(JsonElement),
            "profile", "parity", SnapshotTestLimits.ForState())]));
        using ServiceProvider provider = services.BuildServiceProvider();
        var snapshots = new SnapshotManager(Options.Create(new SnapshotOptions()), NullLogger<SnapshotManager>.Instance, fixture.Service);
        ActorTestContext actor = CreateActor(fixture.Service, stateManager: state, snapshotManager: snapshots,
            serviceProvider: provider, actorId: owner.ActorId);
        fixture.Occurrences.Registry.CompleteWriterAsync(Arg.Any<InteractionOccurrenceIdentity>(), Arg.Any<string>(),
            Arg.Any<string>(), true, Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (!state.CommittedState.ContainsKey(owner.SnapshotKey))
            { return new InteractionOccurrenceReservationResult(InteractionOccurrenceReservationStatus.Unavailable, null); }
            fixture.Occurrences.Record = fixture.Occurrences.Record! with {
                WriterState = InteractionOccurrenceWriterState.Active, WriterProofId = call.ArgAt<string>(2) };
            return new InteractionOccurrenceReservationResult(InteractionOccurrenceReservationStatus.Sealed, fixture.Occurrences.Record);
        });
        state.FaultAfterCall("SaveState", 1, new InvalidOperationException("lost snapshot save acknowledgement"));
        ManualSnapshotResult result = await actor.Actor.CreateManualSnapshotAsync("snapshot");
        result.Outcome.ShouldBe(ManualSnapshotOutcome.Created, result.ReasonCode + "; " + string.Join(" | ", state.Trace));
        fixture.Occurrences.Record!.WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
        state.CommittedState[PayloadProtectionCompletionJournal.StateName].ShouldBeOfType<PayloadProtectionCompletionJournal>()
            .Originals.ShouldHaveSingleItem().Occurrence.PayloadKind.ShouldBe(PayloadProtectionPayloadKind.Snapshot);
    }

    internal static InteractionPayloadProtectionHookFixture Fixture()
    {
        var fixture = new InteractionPayloadProtectionHookFixture();
        fixture.Binding = fixture.Binding with { Identity = fixture.Binding.Identity with {
            Owner = new AggregateIdentity("tenant-a", "d", "interaction-a"), PayloadTypeId = "Legacy.Event" } };
        return fixture;
    }

    internal static EventEvolutionManifestCandidate Candidate()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Hexalith.EventStore.slnx"))) { root = root.Parent; }
        string path = Path.Combine(root!.FullName, "tests", "Hexalith.EventStore.Client.Tests", "Events", "Fixtures", "EventRegistryV17.json");
        var fixture = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!;
        ReadOnlyMemory<byte>[] rows = [Convert.FromHexString(fixture["AliasRow"]), Convert.FromHexString(fixture["DescriptorRow"]),
            Convert.FromHexString(fixture["VersionRow"]), Convert.FromHexString(fixture["SharedRow"])];
        using var registry = new EventDomainRegistry("d", rows);
        return new("d", rows, registry.Fingerprint, 0);
    }

    private static CommandEnvelope Command(InteractionPayloadProtectionHookFixture fixture, string id)
        => new(id, fixture.Binding.Identity.Owner.TenantId, "d", "interaction-a", "Create", [1], id, null, "actor", null);

    private static ActorTestContext Actor(InteractionPayloadProtectionHookFixture fixture, FaultInjectingActorStateManager state,
        IServiceProvider? services = null, ActorTimerManager? scheduler = null, TimeProvider? clock = null)
    {
        ActorTestContext actor = CreateActor(payloadProtectionService: fixture.Service, stateManager: state,
            actorId: fixture.Binding.Identity.Owner.ActorId, serviceProvider: services, timerManager: scheduler, timeProvider: clock);
        actor.AggregateTypeResolver.ResolveAsync(Arg.Any<CommandEnvelope>(), Arg.Any<CancellationToken>()).Returns("r");
        var payload = new SerializedDomainEventPayload("Legacy.Event", InteractionPayloadProtectionHookFixture.Payload(), "json", null, null, null);
        actor.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>()).Returns(DomainResult.Success([payload]));
        return actor;
    }

    private static void ConfigureWriterProof(InteractionPayloadProtectionHookFixture fixture, FaultInjectingActorStateManager state, Func<bool> available)
    {
        fixture.Occurrences.Registry.CompleteWriterAsync(Arg.Any<InteractionOccurrenceIdentity>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var identity = call.ArgAt<InteractionOccurrenceIdentity>(0);
            string key = $"{identity.Owner.EventStreamKeyPrefix}{identity.Sequence}";
            if (!available() || !call.ArgAt<bool>(3) || !state.CommittedState.ContainsKey(key))
            { return new InteractionOccurrenceReservationResult(InteractionOccurrenceReservationStatus.Unavailable, null); }
            fixture.Occurrences.Record = fixture.Occurrences.Record! with {
                WriterState = InteractionOccurrenceWriterState.Active, WriterProofId = call.ArgAt<string>(2) };
            return new InteractionOccurrenceReservationResult(InteractionOccurrenceReservationStatus.Sealed, fixture.Occurrences.Record);
        });
    }
}
