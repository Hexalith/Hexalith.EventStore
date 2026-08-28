using System.Reflection;
using System.Diagnostics.Metrics;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Operations.Actors;
using Hexalith.EventStore.Operations.Configuration;
using Hexalith.EventStore.Operations.Models;
using Hexalith.EventStore.Operations.Replay;
using Hexalith.EventStore.Operations.Telemetry;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Operations.Tests;

/// <summary>
/// Verifies the actor's durable capture, deduplication, conflict, and replay state machine.
/// </summary>
public sealed class DeadLetterDrainActorTests
{
    private static readonly ServiceProvider s_services = new ServiceCollection().AddMetrics().BuildServiceProvider();

    private static readonly PropertyInfo s_stateManagerProperty = typeof(Actor)
        .GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)
        ?? throw new InvalidOperationException("Dapr Actor.StateManager property was not found.");

    /// <summary>Verifies capture commits the raw item and its index before returning.</summary>
    [Fact]
    public async Task CaptureCommitsRawItemAndIndexAtomically()
    {
        (DeadLetterDrainActor actor, InMemoryStateManager stateManager, _) = CreateActor();
        DeadLetterCaptureRequest request = CaptureRequest("message-a", [1, 2, 3]);

        DeadLetterCaptureResult result = await actor.CaptureAsync(request);

        result.Outcome.ShouldBe(DeadLetterCaptureOutcome.Captured);
        stateManager.CommittedState.Keys.ShouldBe([
            DeadLetterDrainActor.IndexStateName,
            DeadLetterDrainActor.ItemStateName("message-a"),
        ], ignoreOrder: true);
        DeadLetterRecord record = stateManager.CommittedState[DeadLetterDrainActor.ItemStateName("message-a")]
            .ShouldBeOfType<DeadLetterRecord>();
        record.Body.ShouldBe([1, 2, 3]);
        stateManager.CommittedState[DeadLetterDrainActor.IndexStateName]
            .ShouldBeOfType<DeadLetterIndex>()
            .MessageIds.ShouldBe(["message-a"]);
    }

    /// <summary>Verifies same-id redelivery deduplicates and conflicting bytes fail closed.</summary>
    [Fact]
    public async Task DuplicateAndHashConflictDoNotCreateAnotherItem()
    {
        (DeadLetterDrainActor actor, InMemoryStateManager stateManager, _) = CreateActor();
        _ = await actor.CaptureAsync(CaptureRequest("message-a", [1, 2, 3]));

        DeadLetterCaptureResult duplicate = await actor.CaptureAsync(CaptureRequest("message-a", [1, 2, 3]));
        DeadLetterCaptureResult conflict = await actor.CaptureAsync(CaptureRequest("message-a", [3, 2, 1]));

        duplicate.Outcome.ShouldBe(DeadLetterCaptureOutcome.Duplicate);
        conflict.Outcome.ShouldBe(DeadLetterCaptureOutcome.HashConflict);
        stateManager.CommittedState.Values.OfType<DeadLetterRecord>().ShouldHaveSingleItem();
    }

    /// <summary>Verifies a persisted replay request converges after reminder recovery.</summary>
    [Fact]
    public async Task ReminderRecoveryReplaysOriginalBytesAndMarksItemReplayed()
    {
        (DeadLetterDrainActor actor, InMemoryStateManager stateManager, IDeadLetterReplayTransport transport) = CreateActor();
        DeadLetterCaptureRequest capture = CaptureRequest("message-a", [1, 2, 3]);
        _ = await actor.CaptureAsync(capture);
        DeadLetterRecord record = stateManager.CommittedState[DeadLetterDrainActor.ItemStateName("message-a")]
            .ShouldBeOfType<DeadLetterRecord>();
        await stateManager.SetStateAsync(
            DeadLetterDrainActor.ItemStateName("message-a"),
            record with { State = DeadLetterReplayState.ReplayRequested });
        await stateManager.SaveStateAsync();

        await actor.ReceiveReminderAsync(DeadLetterDrainActor.ReplayReminderName, [], TimeSpan.Zero, TimeSpan.FromMinutes(1));

        await transport.Received(1).DeliverAsync(
            Arg.Is<byte[]>(body => body.SequenceEqual(new byte[] { 1, 2, 3 })),
            Arg.Any<CancellationToken>());
        DeadLetterRecord replayed = stateManager.CommittedState[DeadLetterDrainActor.ItemStateName("message-a")]
            .ShouldBeOfType<DeadLetterRecord>();
        replayed.State.ShouldBe(DeadLetterReplayState.Replayed);
        replayed.ReplayAttempts.ShouldBe(1);
        replayed.LastReasonCode.ShouldBe("target-acknowledged");
    }

    /// <summary>Verifies tenant mismatch is opaque and cannot mutate the retained item.</summary>
    [Fact]
    public async Task CrossTenantActionRevealsNothingAndDoesNotMutate()
    {
        (DeadLetterDrainActor actor, InMemoryStateManager stateManager, _) = CreateActor();
        _ = await actor.CaptureAsync(CaptureRequest("message-a", [1]));

        DeadLetterActorActionResult result = await actor.ArchiveAsync(
            new DeadLetterActionRequest("tenant-b", ["message-a"]));

        result.ShouldBe(new DeadLetterActorActionResult(false, "not-found"));
        stateManager.CommittedState[DeadLetterDrainActor.ItemStateName("message-a")]
            .ShouldBeOfType<DeadLetterRecord>()
            .State.ShouldBe(DeadLetterReplayState.Pending);
    }

    /// <summary>Verifies an unidentified envelope remains replay-ineligible but can be archived safely.</summary>
    [Fact]
    public async Task UnidentifiedEnvelopeCanBeListedAndArchivedInItsOpaqueScope()
    {
        (DeadLetterDrainActor actor, InMemoryStateManager stateManager, IDeadLetterReplayTransport transport) = CreateActor();
        byte[] body = [0xff, 0x00];
        string hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(body));
        _ = await actor.CaptureAsync(new DeadLetterCaptureRequest(
            new DeadLetterSafeIdentity("unidentified-123", null, null, null, null, null),
            "deadletter.work.events",
            body,
            hash,
            new DateTimeOffset(2026, 8, 28, 10, 0, 0, TimeSpan.Zero)));

        DeadLetterListResult list = await actor.ListAsync(new DeadLetterListRequest(
            DeadLetterSafeIdentity.UnidentifiedTenantId,
            10,
            0));
        DeadLetterActorActionResult retry = await actor.RetryAsync(new DeadLetterActionRequest(
            DeadLetterSafeIdentity.UnidentifiedTenantId,
            ["unidentified-123"]));
        DeadLetterActorActionResult archive = await actor.ArchiveAsync(new DeadLetterActionRequest(
            DeadLetterSafeIdentity.UnidentifiedTenantId,
            ["unidentified-123"]));

        list.Items.ShouldHaveSingleItem();
        retry.ShouldBe(new DeadLetterActorActionResult(false, "invalid-operation"));
        archive.ShouldBe(new DeadLetterActorActionResult(true, "operator-archive"));
        stateManager.CommittedState[DeadLetterDrainActor.ItemStateName("unidentified-123")]
            .ShouldBeOfType<DeadLetterRecord>()
            .State.ShouldBe(DeadLetterReplayState.Archived);
        await transport.DidNotReceiveWithAnyArgs().DeliverAsync(default!);
    }

    /// <summary>Verifies one failed replay cannot starve later requested items in the same drain.</summary>
    [Fact]
    public async Task FailedOldestReplayDoesNotStarveLaterRequestedItem()
    {
        (DeadLetterDrainActor actor, InMemoryStateManager stateManager, IDeadLetterReplayTransport transport) = CreateActor();
        _ = await actor.CaptureAsync(CaptureRequest("message-a", [1]));
        _ = await actor.CaptureAsync(CaptureRequest("message-b", [2]));
        await SetStateAsync(stateManager, "message-a", DeadLetterReplayState.ReplayRequested);
        await SetStateAsync(stateManager, "message-b", DeadLetterReplayState.ReplayRequested);
        _ = transport.DeliverAsync(Arg.Is<byte[]>(body => body.SequenceEqual(new byte[] { 1 })), Arg.Any<CancellationToken>())
            .Returns(_ => throw new HttpRequestException("unavailable"));

        await actor.ReceiveReminderAsync(DeadLetterDrainActor.ReplayReminderName, [], TimeSpan.Zero, TimeSpan.FromMinutes(1));

        await transport.Received(1).DeliverAsync(Arg.Is<byte[]>(body => body.SequenceEqual(new byte[] { 2 })), Arg.Any<CancellationToken>());
        Record(stateManager, "message-a").State.ShouldBe(DeadLetterReplayState.ReplayRequested);
        Record(stateManager, "message-b").State.ShouldBe(DeadLetterReplayState.Replayed);
    }

    /// <summary>Verifies durable replay intent is retained while reminder-registration failure propagates.</summary>
    [Fact]
    public async Task RetryPropagatesReminderRegistrationFailureAfterPersistingIntent()
    {
        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        _ = timerManager.RegisterReminderAsync(Arg.Any<ActorReminder>())
            .Returns(_ => throw new InvalidOperationException("scheduler unavailable"));
        (DeadLetterDrainActor actor, InMemoryStateManager stateManager, IDeadLetterReplayTransport transport) = CreateActor(timerManager);
        _ = await actor.CaptureAsync(CaptureRequest("message-a", [1]));

        _ = await Should.ThrowAsync<InvalidOperationException>(() => actor.RetryAsync(
            new DeadLetterActionRequest("tenant-a", ["message-a"])));

        Record(stateManager, "message-a").State.ShouldBe(DeadLetterReplayState.ReplayRequested);
    }

    /// <summary>Verifies a reminder recovers a persisted in-flight item and saturates attempt arithmetic.</summary>
    [Fact]
    public async Task ReminderNormalizesReplayingAndSaturatesAttemptCount()
    {
        (DeadLetterDrainActor actor, InMemoryStateManager stateManager, IDeadLetterReplayTransport transport) = CreateActor();
        _ = await actor.CaptureAsync(CaptureRequest("message-a", [1]));
        await SetStateAsync(stateManager, "message-a", DeadLetterReplayState.Replaying, int.MaxValue);

        await actor.ReceiveReminderAsync(DeadLetterDrainActor.ReplayReminderName, [], TimeSpan.Zero, TimeSpan.FromMinutes(1));

        await transport.Received(1).DeliverAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        DeadLetterRecord record = Record(stateManager, "message-a");
        record.State.ShouldBe(DeadLetterReplayState.Replayed);
        record.ReplayAttempts.ShouldBe(int.MaxValue);
    }

    /// <summary>Verifies real activation preserves requested intent and normalizes in-flight work before arming recovery.</summary>
    [Theory]
    [InlineData(DeadLetterReplayState.ReplayRequested, null)]
    [InlineData(DeadLetterReplayState.Replaying, "restart-recovery")]
    public async Task ActivationArmsRecoveryForPersistedReplayStates(
        DeadLetterReplayState state,
        string? expectedReason)
    {
        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        (DeadLetterDrainActor actor, InMemoryStateManager stateManager, IDeadLetterReplayTransport transport) = CreateActor(timerManager);
        _ = await actor.CaptureAsync(CaptureRequest("message-a", [1]));
        await SetStateAsync(stateManager, "message-a", state);

        await InvokeOnActivateAsync(actor);

        await timerManager.Received(1).RegisterReminderAsync(Arg.Is<ActorReminder>(value =>
            value.Name == DeadLetterDrainActor.ReplayReminderName));
        DeadLetterRecord record = Record(stateManager, "message-a");
        record.State.ShouldBe(DeadLetterReplayState.ReplayRequested);
        record.LastReasonCode.ShouldBe(expectedReason);
        await actor.ReceiveReminderAsync(DeadLetterDrainActor.ReplayReminderName, [], TimeSpan.Zero, TimeSpan.FromMinutes(1));
        await transport.Received(1).DeliverAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        Record(stateManager, "message-a").State.ShouldBe(DeadLetterReplayState.Replayed);
    }

    /// <summary>Verifies activation fails retryably when durable recovery cannot arm its reminder.</summary>
    [Fact]
    public async Task ActivationPropagatesReminderRegistrationFailure()
    {
        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        _ = timerManager.RegisterReminderAsync(Arg.Any<ActorReminder>())
            .Returns(_ => throw new InvalidOperationException("scheduler unavailable"));
        (DeadLetterDrainActor actor, InMemoryStateManager stateManager, _) = CreateActor(timerManager);
        _ = await actor.CaptureAsync(CaptureRequest("message-a", [1]));
        await SetStateAsync(stateManager, "message-a", DeadLetterReplayState.ReplayRequested);

        _ = await Should.ThrowAsync<InvalidOperationException>(() => InvokeOnActivateAsync(actor));

        Record(stateManager, "message-a").State.ShouldBe(DeadLetterReplayState.ReplayRequested);
    }

    /// <summary>Verifies the raw-index cursor remains stable when an earlier item becomes terminal.</summary>
    [Fact]
    public async Task PaginationCursorSurvivesTerminalMutationBetweenPages()
    {
        (DeadLetterDrainActor actor, _, _) = CreateActor();
        _ = await actor.CaptureAsync(CaptureRequest("message-a", [1]));
        _ = await actor.CaptureAsync(CaptureRequest("message-b", [2]));
        _ = await actor.CaptureAsync(CaptureRequest("message-c", [3]));

        DeadLetterListResult first = await actor.ListAsync(new DeadLetterListRequest("tenant-a", 1, 0));
        _ = await actor.ArchiveAsync(new DeadLetterActionRequest("tenant-a", ["message-a"]));
        DeadLetterListResult second = await actor.ListAsync(new DeadLetterListRequest(
            "tenant-a",
            1,
            first.NextOffset.ShouldNotBeNull()));

        first.Items.ShouldHaveSingleItem().Identity.MessageId.ShouldBe("message-a");
        second.Items.ShouldHaveSingleItem().Identity.MessageId.ShouldBe("message-b");
    }

    /// <summary>Verifies extreme cursors and unknown future states fail closed without overflow.</summary>
    [Fact]
    public async Task ExtremeCursorAndUnknownStateFailClosed()
    {
        (DeadLetterDrainActor actor, InMemoryStateManager stateManager, _) = CreateActor();
        _ = await actor.CaptureAsync(CaptureRequest("message-a", [1]));
        await SetStateAsync(stateManager, "message-a", (DeadLetterReplayState)int.MaxValue);

        DeadLetterListResult result = await actor.ListAsync(new DeadLetterListRequest("tenant-a", 500, int.MaxValue));
        DeadLetterActorActionResult action = await actor.ArchiveAsync(
            new DeadLetterActionRequest("tenant-a", ["message-a"]));

        result.Items.ShouldBeEmpty();
        result.NextOffset.ShouldBeNull();
        action.ShouldBe(new DeadLetterActorActionResult(false, "not-found"));
    }

    /// <summary>Verifies a tenant-filtered list cannot replace the global backlog observation.</summary>
    [Fact]
    public async Task TenantFilteredListDoesNotOverwriteGlobalBacklogObservation()
    {
        var telemetry = new EventStoreOperationsTelemetry(s_services.GetRequiredService<IMeterFactory>(), TimeProvider.System);
        (DeadLetterDrainActor actor, _, _) = CreateActor(telemetry: telemetry);
        _ = await actor.CaptureAsync(CaptureRequest("message-a", [1]));
        DeadLetterCaptureRequest second = CaptureRequest("message-b", [2]);
        _ = await actor.CaptureAsync(second with
        {
            Identity = second.Identity with { TenantId = "tenant-b" },
        });

        _ = await actor.ListAsync(new DeadLetterListRequest("tenant-a", 10, 0));

        telemetry.CurrentBacklog.Count.ShouldBe(2);
    }

    private static DeadLetterCaptureRequest CaptureRequest(string messageId, byte[] body)
    {
        string hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(body));
        return new DeadLetterCaptureRequest(
            new DeadLetterSafeIdentity(messageId, "tenant-a", "work", "work-a", "correlation-a", "WorkItemCreated"),
            "deadletter.work.events",
            body,
            hash,
            new DateTimeOffset(2026, 8, 28, 10, 0, 0, TimeSpan.Zero));
    }

    private static (DeadLetterDrainActor Actor, InMemoryStateManager StateManager, IDeadLetterReplayTransport Transport) CreateActor(
        ActorTimerManager? timerManager = null,
        EventStoreOperationsTelemetry? telemetry = null)
    {
        var stateManager = new InMemoryStateManager();
        timerManager ??= Substitute.For<ActorTimerManager>();
        var host = ActorHost.CreateForTest<DeadLetterDrainActor>(new ActorTestOptions
        {
            ActorId = new ActorId("deadletter.work.events"),
            TimerManager = timerManager,
        });
        IDeadLetterReplayTransport transport = Substitute.For<IDeadLetterReplayTransport>();
        telemetry ??= new EventStoreOperationsTelemetry(s_services.GetRequiredService<IMeterFactory>(), TimeProvider.System);
        var actor = new DeadLetterDrainActor(
            host,
            transport,
            telemetry,
            Options.Create(new EventStoreOperationsOptions()));
        s_stateManagerProperty.SetValue(actor, stateManager);
        return (actor, stateManager, transport);
    }

    private static Task InvokeOnActivateAsync(DeadLetterDrainActor actor)
    {
        MethodInfo method = typeof(DeadLetterDrainActor)
            .GetMethod("OnActivateAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("DeadLetterDrainActor.OnActivateAsync was not found.");
        return (Task)method.Invoke(actor, null)!;
    }

    private static DeadLetterRecord Record(InMemoryStateManager stateManager, string messageId)
        => stateManager.CommittedState[DeadLetterDrainActor.ItemStateName(messageId)]
            .ShouldBeOfType<DeadLetterRecord>();

    private static async Task SetStateAsync(
        InMemoryStateManager stateManager,
        string messageId,
        DeadLetterReplayState state,
        int? replayAttempts = null)
    {
        DeadLetterRecord record = Record(stateManager, messageId);
        await stateManager.SetStateAsync(
            DeadLetterDrainActor.ItemStateName(messageId),
            record with
            {
                State = state,
                ReplayAttempts = replayAttempts ?? record.ReplayAttempts,
            });
        await stateManager.SaveStateAsync();
    }
}
