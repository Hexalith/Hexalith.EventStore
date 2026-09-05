
using System.Reflection;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>
/// Story 4.4: committed-event publication recovery.
/// Covers every row of the story I/O matrix for the activation hook plus the per-creation-site
/// proof that a drain record always has a matching recovery index entry.
/// <para>
/// Deliberate separation of concerns: a test that <em>seeds</em> the index re-stubs
/// <c>TryGetStateAsync&lt;UnpublishedPublicationIndex&gt;</c> with a fixed value and therefore
/// cannot also prove that staging happened. Seeding tests assert re-arm/prune behavior; staging
/// tests never seed.
/// </para>
/// </summary>
public class PublicationRecoveryActivationTests {
    private const string ActorId = "test-tenant:test-domain:agg-001";
    private const string PipelineKeyPrefix = "test-tenant:test-domain:agg-001:pipeline:";

    private sealed record ActivationContext(
        AggregateActor Actor,
        IActorStateManager StateManager,
        ActorTimerManager TimerManager,
        IEventPublisher EventPublisher,
        IDeadLetterPublisher DeadLetterPublisher,
        IDomainServiceInvoker Invoker);

    private static ActivationContext CreateActorForBoundedDrain(
        EventDrainOptions? drainOptions = null,
        IActorStateManager? stateManager = null) {
        stateManager ??= Substitute.For<IActorStateManager>();
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore statusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();
        IDeadLetterPublisher deadLetterPublisher = Substitute.For<IDeadLetterPublisher>();
        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId(ActorId), TimerManager = timerManager });
        var actor = new AggregateActor(
            host,
            logger,
            invoker,
            snapshotManager,
            new NoOpEventPayloadProtectionService(),
            statusStore,
            eventPublisher,
            Options.Create(drainOptions ?? new EventDrainOptions()),
            Options.Create(new BackpressureOptions()),
            deadLetterPublisher);

        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);

        return new ActivationContext(actor, stateManager, timerManager, eventPublisher, deadLetterPublisher, invoker);
    }

    /// <summary>Configures the command-processing defaults required to drive a full command turn.</summary>
    private static void ConfigureCommandDefaults(ActivationContext context, int domainEventCount = 1) {
        _ = context.StateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));
        _ = context.StateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));
        _ = context.StateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(false, default!));

        TestEvent[] events = [.. Enumerable.Range(0, domainEventCount).Select(_ => new TestEvent())];
        _ = context.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .Returns(DomainResult.Success(events));

        // Default: publication succeeds. Individual tests override this with a failure result.
        _ = context.EventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(callInfo => new EventPublishResult(true, callInfo.ArgAt<IReadOnlyList<EventEnvelope>>(1).Count, null));
    }

    private static CommandEnvelope CreateEnvelope(
        string messageId = "msg-primary",
        string correlationId = "corr-primary") => new(
        MessageId: messageId,
        TenantId: "test-tenant",
        Domain: "test-domain",
        AggregateId: "agg-001",
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: correlationId,
        CausationId: null,
        UserId: "system",
        Extensions: null);

    private static void SeedPublicationIndex(
        IActorStateManager stateManager,
        params UnpublishedPublicationEntry[] entries)
        => _ = stateManager.TryGetStateAsync<UnpublishedPublicationIndex>(
            UnpublishedPublicationIndex.StateKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedPublicationIndex>(true, new UnpublishedPublicationIndex(entries)));

    private static UnpublishedPublicationEntry Entry(string messageId, string correlationId)
        => new(messageId, correlationId, DateTimeOffset.UtcNow);

    private static void SeedCheckpoint(
        IActorStateManager stateManager,
        string correlationId,
        PipelineState checkpoint)
        => _ = stateManager.TryGetStateAsync<PipelineState>(
            $"{PipelineKeyPrefix}{correlationId}", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(true, checkpoint));

    private static void SeedDrainRecord(
        IActorStateManager stateManager,
        string trackingId,
        UnpublishedEventsRecord record)
        => _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            UnpublishedEventsRecord.GetStateKey(trackingId), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

    private static void SeedRecoverableIdempotency(IActorStateManager stateManager, string messageId)
        => _ = stateManager.TryGetStateAsync<IdempotencyRecord>(
            $"idempotency:{messageId}", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(true, new IdempotencyRecord(
                messageId,
                "corr-x",
                Accepted: true,
                ErrorMessage: null,
                ProcessedAt: DateTimeOffset.UtcNow.AddMinutes(-10),
                MessageId: messageId,
                ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-1),
                Disposition: IdempotencyRecordDisposition.Recoverable)));

    /// <summary>Asserts the Recoverable -> Terminal transition was staged for a message id.</summary>
    private static async Task ShouldHaveReleasedRecoverableIdempotencyAsync(
        IActorStateManager stateManager,
        string messageId)
        => await stateManager.Received(1).SetStateAsync(
            $"idempotency:{messageId}",
            Arg.Is<IdempotencyRecord>(r => r.Disposition == IdempotencyRecordDisposition.Terminal),
            Arg.Any<CancellationToken>());

    /// <summary>Mirrors AggregateActor.MaxActivationProbeEntries, which is private.</summary>
    private const int MaxActivationProbeEntries = 32;

    private static int CountStateReads(IActorStateManager stateManager, string keyPrefix)
        => stateManager.ReceivedCalls()
            .Count(c => c.GetMethodInfo().Name == nameof(IActorStateManager.TryGetStateAsync)
                && c.GetArguments().Length > 0
                && c.GetArguments()[0] is string key
                && key.StartsWith(keyPrefix, StringComparison.Ordinal));

    private static Task InvokeOnActivateAsync(AggregateActor actor) {
        MethodInfo method = typeof(AggregateActor)
            .GetMethod("OnActivateAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("AggregateActor.OnActivateAsync was not found.");
        return (Task)method.Invoke(actor, null)!;
    }

    // ===== PR1: pruning ends the Recoverable exemption from bounded expiry =====
    // A pruned entry means the events are no longer outstanding. Leaving the idempotency record
    // Recoverable makes it immortal (IdempotencyChecker.IsExpired exempts Recoverable
    // unconditionally), so every later retry of that message id returns RetryableRecoverable
    // forever -- the exact failure this story was reverted for once already.

    [Fact]
    public async Task OnActivate_MalformedEntryPruned_ReleasesTheRecoverableIdempotencyRecord() {
        ActivationContext ctx = CreateActorForBoundedDrain();
        SeedPublicationIndex(ctx.StateManager, Entry("msg-malformed", string.Empty));
        SeedRecoverableIdempotency(ctx.StateManager, "msg-malformed");

        await InvokeOnActivateAsync(ctx.Actor);

        await ShouldHaveReleasedRecoverableIdempotencyAsync(ctx.StateManager, "msg-malformed");
    }

    [Fact]
    public async Task OnActivate_BlankMessageIdEntry_PrunesWithoutThrowing() {
        // CompleteRecoverableIdempotencyAsync must absorb the blank id; TryCompleteRecoverableAsync
        // throws ArgumentException on one, which would brick the activation hook.
        ActivationContext ctx = CreateActorForBoundedDrain();
        SeedPublicationIndex(ctx.StateManager, Entry(string.Empty, "corr-blank"));

        await Should.NotThrowAsync(() => InvokeOnActivateAsync(ctx.Actor));

        await ctx.StateManager.Received(1).SetStateAsync(
            UnpublishedPublicationIndex.StateKey,
            Arg.Is<UnpublishedPublicationIndex>(i => i.Entries.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActivate_MissingCheckpointPruned_ReleasesTheRecoverableIdempotencyRecord() {
        ActivationContext ctx = CreateActorForBoundedDrain();
        SeedPublicationIndex(ctx.StateManager, Entry("msg-nocheckpoint", "corr-nocheckpoint"));
        SeedRecoverableIdempotency(ctx.StateManager, "msg-nocheckpoint");

        await InvokeOnActivateAsync(ctx.Actor);

        await ShouldHaveReleasedRecoverableIdempotencyAsync(ctx.StateManager, "msg-nocheckpoint");
    }

    [Fact]
    public async Task OnActivate_IncompleteCheckpointPruned_ReleasesTheRecoverableIdempotencyRecord() {
        ActivationContext ctx = CreateActorForBoundedDrain();
        SeedPublicationIndex(ctx.StateManager, Entry("msg-badrange2", "corr-badrange2"));
        SeedRecoverableIdempotency(ctx.StateManager, "msg-badrange2");
        SeedCheckpoint(ctx.StateManager, "corr-badrange2", new PipelineState(
            "corr-badrange2",
            CommandStatus.EventsStored,
            "CreateOrder",
            DateTimeOffset.UtcNow.AddMinutes(-1),
            EventCount: 2,
            RejectionEventType: null,
            MessageId: "msg-badrange2",
            CausationId: "msg-badrange2",
            StartSequence: 4,
            EndSequence: 9));

        await InvokeOnActivateAsync(ctx.Actor);

        await ShouldHaveReleasedRecoverableIdempotencyAsync(ctx.StateManager, "msg-badrange2");
    }

    // ===== Checkpoint stage is load-bearing and is not enforced downstream =====

    [Fact]
    public async Task OnActivate_AlreadyPublishedCheckpoint_IsPrunedRatherThanRepublished() {
        // HandoffStaleCommittedCheckpointAsync validates identity, event count and range but NOT the
        // stage, and its other caller admits Completed/EventsPublished. Without the stage term a
        // terminal batch that failed after publishing would leave a Completed checkpoint plus a live
        // index entry, and activation would rebuild a drain record and publish the range twice.
        ActivationContext ctx = CreateActorForBoundedDrain();
        SeedPublicationIndex(ctx.StateManager, Entry("msg-published", "corr-published"));
        SeedCheckpoint(ctx.StateManager, "corr-published", new PipelineState(
            "corr-published",
            CommandStatus.Completed,
            "CreateOrder",
            DateTimeOffset.UtcNow.AddMinutes(-1),
            EventCount: 2,
            RejectionEventType: null,
            MessageId: "msg-published",
            CausationId: "msg-published",
            StartSequence: 1,
            EndSequence: 2));

        await InvokeOnActivateAsync(ctx.Actor);

        await ctx.StateManager.DidNotReceive().SetStateAsync(
            "drain:msg-published",
            Arg.Any<UnpublishedEventsRecord>(),
            Arg.Any<CancellationToken>());
        await ctx.TimerManager.DidNotReceive().RegisterReminderAsync(Arg.Any<ActorReminder>());
        await ctx.StateManager.Received(1).SetStateAsync(
            UnpublishedPublicationIndex.StateKey,
            Arg.Is<UnpublishedPublicationIndex>(i => i.Entries.Count == 0),
            Arg.Any<CancellationToken>());
    }

    // ===== PR6: already-armed entries must not starve the unarmed tail =====

    [Fact]
    public async Task OnActivate_ArmedHeadDoesNotStarveUnarmedTailBeyondFormerProbeBound() {
        // Regression: charging ReminderArmedAt skips against MaxActivationProbeEntries left every
        // unarmed entry past index slot 32 permanently unexamined on a continuously active actor.
        const int armedCount = MaxActivationProbeEntries;
        ActivationContext ctx = CreateActorForBoundedDrain();
        var entries = new List<UnpublishedPublicationEntry>();
        for (int i = 0; i < armedCount; i++) {
            entries.Add(Entry($"msg-p{i:D3}", $"corr-p{i:D3}"));
            SeedDrainRecord(ctx.StateManager, $"msg-p{i:D3}", new UnpublishedEventsRecord(
                $"corr-p{i:D3}", 1, 1, 1, "CreateOrder", false, DateTimeOffset.UtcNow,
                RetryCount: 0, LastFailureReason: null, MessageId: $"msg-p{i:D3}",
                DeadLettered: false, ReminderArmedAt: DateTimeOffset.UtcNow.AddMinutes(-1)));
        }

        entries.Add(Entry("msg-tail", "corr-tail"));
        SeedDrainRecord(ctx.StateManager, "msg-tail", new UnpublishedEventsRecord(
            "corr-tail", 1, 1, 1, "CreateOrder", false, DateTimeOffset.UtcNow,
            RetryCount: 0, LastFailureReason: null, MessageId: "msg-tail"));

        SeedPublicationIndex(ctx.StateManager, [.. entries]);

        await InvokeOnActivateAsync(ctx.Actor);

        await ctx.TimerManager.Received(1).RegisterReminderAsync(
            Arg.Is<ActorReminder>(r => r.Name == "drain-unpublished-msg-tail"));
    }

    [Fact]
    public async Task OnActivate_EntriesWithoutDrainRecords_AreChargedOneProbeEach() {
        // Regression guard for a double-charged probe budget. An entry with no drain record costs a
        // drain read AND a checkpoint read; if both are charged, the effective ceiling halves and
        // the scan silently stops after 16 entries instead of 32, leaving the tail unexamined for
        // this activation. Armed entries cannot catch this -- they return before the second charge.
        const int seeded = 40;
        ActivationContext ctx = CreateActorForBoundedDrain();
        var entries = new List<UnpublishedPublicationEntry>();
        for (int i = 0; i < seeded; i++) {
            entries.Add(Entry($"msg-q{i:D3}", $"corr-q{i:D3}"));
        }

        SeedPublicationIndex(ctx.StateManager, [.. entries]);

        await InvokeOnActivateAsync(ctx.Actor);

        // Probe is charged only after the drain classifying read proves the entry is not already
        // armed. Hitting the bound therefore performs one extra drain peek, then breaks before the
        // checkpoint load — so drain reads are bound+1 and checkpoint reads equal the bound.
        CountStateReads(ctx.StateManager, "drain:").ShouldBe(MaxActivationProbeEntries + 1);
        CountStateReads(ctx.StateManager, PipelineKeyPrefix).ShouldBe(MaxActivationProbeEntries);
    }

    // ===== I/O matrix row: crash after event commit, before drain record =====

    [Fact]
    public async Task OnActivate_CommittedCheckpointWithoutDrainRecord_RebuildsDrainRecordAndRegistersReminder() {
        ActivationContext ctx = CreateActorForBoundedDrain();
        SeedPublicationIndex(ctx.StateManager, Entry("msg-orphan", "corr-orphan"));
        SeedCheckpoint(ctx.StateManager, "corr-orphan", new PipelineState(
            "corr-orphan",
            CommandStatus.EventsStored,
            "CreateOrder",
            DateTimeOffset.UtcNow.AddMinutes(-1),
            EventCount: 2,
            RejectionEventType: null,
            MessageId: "msg-orphan",
            CausationId: "msg-orphan",
            StartSequence: 4,
            EndSequence: 5));

        await InvokeOnActivateAsync(ctx.Actor);

        // The rebuilt record is written twice: once by the handoff and once by the post-registration
        // ReminderArmedAt stamp. Pin the rebuilt range on the first (unstamped) write.
        await ctx.StateManager.Received(1).SetStateAsync(
            "drain:msg-orphan",
            Arg.Is<UnpublishedEventsRecord>(r =>
                r.CorrelationId == "corr-orphan"
                && r.MessageId == "msg-orphan"
                && r.StartSequence == 4
                && r.EndSequence == 5
                && r.EventCount == 2
                && r.RetryCount == 0
                && r.ReminderArmedAt == null),
            Arg.Any<CancellationToken>());
        await ctx.TimerManager.Received(1).RegisterReminderAsync(
            Arg.Is<ActorReminder>(r => r.Name == "drain-unpublished-msg-orphan"));

        // ...and the stamp follows, so the next activation does not re-register this live reminder.
        await ctx.StateManager.Received(1).SetStateAsync(
            "drain:msg-orphan",
            Arg.Is<UnpublishedEventsRecord>(r => r.ReminderArmedAt != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActivate_CommittedCheckpointWithoutDrainRecord_DoesNotPublishDuringActivation() {
        ActivationContext ctx = CreateActorForBoundedDrain();
        SeedPublicationIndex(ctx.StateManager, Entry("msg-orphan", "corr-orphan"));
        SeedCheckpoint(ctx.StateManager, "corr-orphan", new PipelineState(
            "corr-orphan",
            CommandStatus.EventsStored,
            "CreateOrder",
            DateTimeOffset.UtcNow.AddMinutes(-1),
            EventCount: 2,
            RejectionEventType: null,
            MessageId: "msg-orphan",
            CausationId: "msg-orphan",
            StartSequence: 4,
            EndSequence: 5));

        await InvokeOnActivateAsync(ctx.Actor);

        // Activation re-arms only. Publication belongs to the reminder path.
        _ = await ctx.EventPublisher.DidNotReceive().PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>());
        _ = await ctx.DeadLetterPublisher.DidNotReceive().PublishDeadLetterAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<DeadLetterMessage>(),
            Arg.Any<CancellationToken>());
    }

    // ===== I/O matrix row: checkpoint missing or lacking the persisted range =====

    [Fact]
    public async Task OnActivate_CheckpointMissing_DropsStaleEntryWithoutFabricatingRange() {
        ActivationContext ctx = CreateActorForBoundedDrain();
        SeedPublicationIndex(ctx.StateManager, Entry("msg-gone", "corr-gone"));

        await InvokeOnActivateAsync(ctx.Actor);

        await ctx.StateManager.DidNotReceive().SetStateAsync(
            Arg.Is<string>(s => s.StartsWith("drain:", StringComparison.Ordinal)),
            Arg.Any<UnpublishedEventsRecord>(),
            Arg.Any<CancellationToken>());
        await ctx.StateManager.Received(1).SetStateAsync(
            UnpublishedPublicationIndex.StateKey,
            Arg.Is<UnpublishedPublicationIndex>(i => i.Entries.Count == 0),
            Arg.Any<CancellationToken>());
        await ctx.StateManager.Received().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null, 2L)]
    [InlineData(1L, null)]
    public async Task OnActivate_CheckpointWithoutPersistedRange_DropsStaleEntryWithoutFabricatingRange(
        long? startSequence,
        long? endSequence) {
        ActivationContext ctx = CreateActorForBoundedDrain();
        SeedPublicationIndex(ctx.StateManager, Entry("msg-legacy", "corr-legacy"));
        SeedCheckpoint(ctx.StateManager, "corr-legacy", new PipelineState(
            "corr-legacy",
            CommandStatus.EventsStored,
            "CreateOrder",
            DateTimeOffset.UtcNow.AddMinutes(-1),
            EventCount: 2,
            RejectionEventType: null,
            MessageId: "msg-legacy",
            CausationId: "msg-legacy",
            StartSequence: startSequence,
            EndSequence: endSequence));

        await InvokeOnActivateAsync(ctx.Actor);

        await ctx.StateManager.DidNotReceive().SetStateAsync(
            Arg.Is<string>(s => s.StartsWith("drain:", StringComparison.Ordinal)),
            Arg.Any<UnpublishedEventsRecord>(),
            Arg.Any<CancellationToken>());
        await ctx.StateManager.Received(1).SetStateAsync(
            UnpublishedPublicationIndex.StateKey,
            Arg.Is<UnpublishedPublicationIndex>(i => i.Entries.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task OnActivate_CheckpointWithoutCommittedEvents_DropsStaleEntry(int? eventCount) {
        ActivationContext ctx = CreateActorForBoundedDrain();
        SeedPublicationIndex(ctx.StateManager, Entry("msg-noevents", "corr-noevents"));
        SeedCheckpoint(ctx.StateManager, "corr-noevents", new PipelineState(
            "corr-noevents",
            CommandStatus.Processing,
            "CreateOrder",
            DateTimeOffset.UtcNow.AddMinutes(-1),
            EventCount: eventCount,
            RejectionEventType: null,
            MessageId: "msg-noevents",
            CausationId: "msg-noevents",
            StartSequence: 1,
            EndSequence: 2));

        await InvokeOnActivateAsync(ctx.Actor);

        await ctx.StateManager.DidNotReceive().SetStateAsync(
            Arg.Is<string>(s => s.StartsWith("drain:", StringComparison.Ordinal)),
            Arg.Any<UnpublishedEventsRecord>(),
            Arg.Any<CancellationToken>());
        await ctx.StateManager.Received(1).SetStateAsync(
            UnpublishedPublicationIndex.StateKey,
            Arg.Is<UnpublishedPublicationIndex>(i => i.Entries.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActivate_CheckpointBelongsToAnotherMessage_DropsStaleEntry() {
        ActivationContext ctx = CreateActorForBoundedDrain();
        SeedPublicationIndex(ctx.StateManager, Entry("msg-mine", "corr-shared"));
        SeedCheckpoint(ctx.StateManager, "corr-shared", new PipelineState(
            "corr-shared",
            CommandStatus.EventsStored,
            "CreateOrder",
            DateTimeOffset.UtcNow.AddMinutes(-1),
            EventCount: 2,
            RejectionEventType: null,
            MessageId: "msg-someone-else",
            CausationId: "msg-someone-else",
            StartSequence: 1,
            EndSequence: 2));

        await InvokeOnActivateAsync(ctx.Actor);

        await ctx.StateManager.DidNotReceive().SetStateAsync(
            Arg.Is<string>(s => s.StartsWith("drain:", StringComparison.Ordinal)),
            Arg.Any<UnpublishedEventsRecord>(),
            Arg.Any<CancellationToken>());
        await ctx.StateManager.Received(1).SetStateAsync(
            UnpublishedPublicationIndex.StateKey,
            Arg.Is<UnpublishedPublicationIndex>(i => i.Entries.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActivate_MalformedEntry_IsPrunedRatherThanSkipped() {
        ActivationContext ctx = CreateActorForBoundedDrain();
        SeedPublicationIndex(
            ctx.StateManager,
            new UnpublishedPublicationEntry(string.Empty, "corr-blank", DateTimeOffset.UtcNow));

        await InvokeOnActivateAsync(ctx.Actor);

        // Skipping without pruning would permanently consume index capacity.
        await ctx.StateManager.Received(1).SetStateAsync(
            UnpublishedPublicationIndex.StateKey,
            Arg.Is<UnpublishedPublicationIndex>(i => i.Entries.Count == 0),
            Arg.Any<CancellationToken>());
        await ctx.StateManager.DidNotReceive().TryGetStateAsync<UnpublishedEventsRecord>(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ===== I/O matrix row: crash after drain commit, before reminder =====

    [Fact]
    public async Task OnActivate_DrainRecordWithoutReminder_ReRegistersReminderAndLeavesRetryCountUnchanged() {
        ActivationContext ctx = CreateActorForBoundedDrain();
        SeedPublicationIndex(ctx.StateManager, Entry("msg-armed", "corr-armed"));
        SeedDrainRecord(ctx.StateManager, "msg-armed", new UnpublishedEventsRecord(
            "corr-armed", 1, 2, 2, "CreateOrder", false, DateTimeOffset.UtcNow,
            RetryCount: 3, LastFailureReason: "pubsub down", MessageId: "msg-armed"));

        await InvokeOnActivateAsync(ctx.Actor);

        await ctx.TimerManager.Received(1).RegisterReminderAsync(
            Arg.Is<ActorReminder>(r => r.Name == "drain-unpublished-msg-armed"));

        // The record is stamped so no later activation resets the live schedule, and RetryCount is
        // left exactly as persisted — activation must not consume an attempt.
        await ctx.StateManager.Received(1).SetStateAsync(
            "drain:msg-armed",
            Arg.Is<UnpublishedEventsRecord>(r => r.RetryCount == 3 && r.ReminderArmedAt != null),
            Arg.Any<CancellationToken>());

        // A live entry is not pruned.
        await ctx.StateManager.DidNotReceive().SetStateAsync(
            UnpublishedPublicationIndex.StateKey,
            Arg.Any<UnpublishedPublicationIndex>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActivate_DrainRecordAlreadyArmed_DoesNotResetTheLiveReminderSchedule() {
        // Re-registering resets dueTime to InitialDrainDelay. An aggregate that activates more often
        // than that delay would postpone its own drain indefinitely.
        ActivationContext ctx = CreateActorForBoundedDrain();
        SeedPublicationIndex(ctx.StateManager, Entry("msg-live", "corr-live"));
        SeedDrainRecord(ctx.StateManager, "msg-live", new UnpublishedEventsRecord(
            "corr-live", 1, 2, 2, "CreateOrder", false, DateTimeOffset.UtcNow,
            RetryCount: 1, LastFailureReason: "pubsub down", MessageId: "msg-live",
            DeadLettered: false, ReminderArmedAt: DateTimeOffset.UtcNow.AddMinutes(-5)));

        await InvokeOnActivateAsync(ctx.Actor);

        await ctx.TimerManager.DidNotReceive().RegisterReminderAsync(Arg.Any<ActorReminder>());
        await ctx.StateManager.DidNotReceive().SetStateAsync(
            "drain:msg-live", Arg.Any<UnpublishedEventsRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActivate_AlreadyArmedEntries_DoNotConsumeTheReArmBudget() {
        // Regression guard: counting every examined entry against the re-arm budget let the first
        // eight already-armed entries starve every entry behind them on a hot aggregate.
        const int armedCount = 8;
        const int unarmedCount = 3;
        ActivationContext ctx = CreateActorForBoundedDrain();
        var entries = new List<UnpublishedPublicationEntry>();
        for (int i = 0; i < armedCount; i++) {
            entries.Add(Entry($"msg-armed-{i:D2}", $"corr-armed-{i:D2}"));
            SeedDrainRecord(ctx.StateManager, $"msg-armed-{i:D2}", new UnpublishedEventsRecord(
                $"corr-armed-{i:D2}", 1, 1, 1, "CreateOrder", false, DateTimeOffset.UtcNow,
                RetryCount: 0, LastFailureReason: null, MessageId: $"msg-armed-{i:D2}",
                DeadLettered: false, ReminderArmedAt: DateTimeOffset.UtcNow.AddMinutes(-1)));
        }

        for (int i = 0; i < unarmedCount; i++) {
            entries.Add(Entry($"msg-new-{i:D2}", $"corr-new-{i:D2}"));
            SeedDrainRecord(ctx.StateManager, $"msg-new-{i:D2}", new UnpublishedEventsRecord(
                $"corr-new-{i:D2}", 1, 1, 1, "CreateOrder", false, DateTimeOffset.UtcNow,
                RetryCount: 0, LastFailureReason: null, MessageId: $"msg-new-{i:D2}"));
        }

        SeedPublicationIndex(ctx.StateManager, [.. entries]);

        await InvokeOnActivateAsync(ctx.Actor);

        for (int i = 0; i < unarmedCount; i++) {
            string expected = $"drain-unpublished-msg-new-{i:D2}";
            await ctx.TimerManager.Received(1).RegisterReminderAsync(
                Arg.Is<ActorReminder>(r => r.Name == expected));
        }

        await ctx.TimerManager.Received(unarmedCount).RegisterReminderAsync(Arg.Any<ActorReminder>());
    }

    [Fact]
    public async Task OnActivate_ReminderRegistrationThrows_DoesNotStampTheRecordAsArmed() {
        ActivationContext ctx = CreateActorForBoundedDrain();
        SeedPublicationIndex(ctx.StateManager, Entry("msg-armed", "corr-armed"));
        SeedDrainRecord(ctx.StateManager, "msg-armed", new UnpublishedEventsRecord(
            "corr-armed", 1, 2, 2, "CreateOrder", false, DateTimeOffset.UtcNow,
            RetryCount: 1, LastFailureReason: "pubsub down", MessageId: "msg-armed"));
        _ = ctx.TimerManager.RegisterReminderAsync(Arg.Any<ActorReminder>())
            .Returns(Task.FromException(new InvalidOperationException("reminder store unavailable")));

        await Should.NotThrowAsync(() => InvokeOnActivateAsync(ctx.Actor));

        // Registration WAS attempted...
        await ctx.TimerManager.Received(1).RegisterReminderAsync(
            Arg.Is<ActorReminder>(r => r.Name == "drain-unpublished-msg-armed"));

        // ...and because it failed, the record must NOT be stamped armed. This is what distinguishes
        // the throwing case from the succeeding one, which does write the stamp.
        await ctx.StateManager.DidNotReceive().SetStateAsync(
            "drain:msg-armed",
            Arg.Is<UnpublishedEventsRecord>(r => r.ReminderArmedAt != null),
            Arg.Any<CancellationToken>());

        // Entry and record survive for the next activation.
        await ctx.StateManager.DidNotReceive().RemoveStateAsync(
            "drain:msg-armed", Arg.Any<CancellationToken>());
        await ctx.StateManager.DidNotReceive().SetStateAsync(
            UnpublishedPublicationIndex.StateKey,
            Arg.Any<UnpublishedPublicationIndex>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActivate_CheckpointRangeInconsistentWithEventCount_IsPrunedRatherThanThrowingForever() {
        // EventCount 2 but a 6-wide range: HandoffStaleCommittedCheckpointAsync would throw on every
        // activation, and the catch does not prune, so the entry would hold a re-arm slot and an
        // index capacity slot permanently.
        ActivationContext ctx = CreateActorForBoundedDrain();
        SeedPublicationIndex(ctx.StateManager, Entry("msg-badrange", "corr-badrange"));
        SeedCheckpoint(ctx.StateManager, "corr-badrange", new PipelineState(
            "corr-badrange",
            CommandStatus.EventsStored,
            "CreateOrder",
            DateTimeOffset.UtcNow.AddMinutes(-1),
            EventCount: 2,
            RejectionEventType: null,
            MessageId: "msg-badrange",
            CausationId: "msg-badrange",
            StartSequence: 4,
            EndSequence: 9));

        await InvokeOnActivateAsync(ctx.Actor);

        await ctx.StateManager.DidNotReceive().SetStateAsync(
            Arg.Is<string>(s => s.StartsWith("drain:", StringComparison.Ordinal)),
            Arg.Any<UnpublishedEventsRecord>(),
            Arg.Any<CancellationToken>());
        await ctx.StateManager.Received(1).SetStateAsync(
            UnpublishedPublicationIndex.StateKey,
            Arg.Is<UnpublishedPublicationIndex>(i => i.Entries.Count == 0),
            Arg.Any<CancellationToken>());
    }

    // ===== I/O matrix row: activation with nothing outstanding =====

    [Fact]
    public async Task OnActivate_NoOutstandingEntries_ReconcilesWithoutSaving() {
        ActivationContext ctx = CreateActorForBoundedDrain();
        _ = ctx.StateManager.TryGetStateAsync<UnpublishedPublicationIndex>(
            UnpublishedPublicationIndex.StateKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedPublicationIndex>(false, default!));

        await InvokeOnActivateAsync(ctx.Actor);

        await ctx.StateManager.Received(2).TryGetStateAsync<UnpublishedPublicationIndex>(
            UnpublishedPublicationIndex.StateKey, Arg.Any<CancellationToken>());
        await ctx.StateManager.Received(1).ClearCacheAsync(Arg.Any<CancellationToken>());
        await ctx.StateManager.Received(1).TryGetStateAsync<int>(
            "pending_command_count", Arg.Any<CancellationToken>());
        await ctx.StateManager.DidNotReceive().TryGetStateAsync<UnpublishedEventsRecord>(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
        await ctx.StateManager.DidNotReceive().TryGetStateAsync<PipelineState>(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
        await ctx.StateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
        await ctx.TimerManager.DidNotReceive().RegisterReminderAsync(Arg.Any<ActorReminder>());
    }

    [Fact]
    public async Task OnActivate_NonemptyIndex_ReconcilesPendingCountToDistinctOwners() {
        ActivationContext ctx = CreateActorForBoundedDrain();
        UnpublishedPublicationEntry[] entries = [
            Entry("msg-owner-1", "corr-owner-1"),
            Entry("msg-owner-2", "corr-owner-2"),
        ];
        SeedPublicationIndex(ctx.StateManager, entries);
        foreach (UnpublishedPublicationEntry entry in entries) {
            SeedDrainRecord(ctx.StateManager, entry.MessageId, new UnpublishedEventsRecord(
                entry.CorrelationId, 1, 1, 1, "CreateOrder", false, DateTimeOffset.UtcNow,
                RetryCount: 0, LastFailureReason: null, MessageId: entry.MessageId,
                ReminderArmedAt: DateTimeOffset.UtcNow));
        }

        _ = ctx.StateManager.TryGetStateAsync<int>(
            "pending_command_count", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<int>(true, 7));

        await InvokeOnActivateAsync(ctx.Actor);

        await ctx.StateManager.Received(1).SetStateAsync(
            "pending_command_count", 2, Arg.Any<CancellationToken>());
        await ctx.StateManager.Received(1).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActivate_ReconciliationSaveCommitsThenThrows_ObservesCommitWithoutSecondSave() {
        var stateManager = new FaultInjectingActorStateManager();
        var index = new UnpublishedPublicationIndex([
            Entry("msg-owner-1", "corr-owner-1"),
            Entry("msg-owner-2", "corr-owner-2"),
        ]);
        await stateManager.SeedCommittedStateAsync(new Dictionary<string, object> {
            [UnpublishedPublicationIndex.StateKey] = index,
            ["pending_command_count"] = 0,
            ["drain:msg-owner-1"] = new UnpublishedEventsRecord(
                "corr-owner-1", 1, 1, 1, "CreateOrder", false, DateTimeOffset.UtcNow,
                0, null, "msg-owner-1", ReminderArmedAt: DateTimeOffset.UtcNow),
            ["drain:msg-owner-2"] = new UnpublishedEventsRecord(
                "corr-owner-2", 2, 2, 1, "CreateOrder", false, DateTimeOffset.UtcNow,
                0, null, "msg-owner-2", ReminderArmedAt: DateTimeOffset.UtcNow),
        });
        stateManager.FaultAfterCall("SaveState", 1, new InvalidOperationException("commit uncertain"));
        ActivationContext ctx = CreateActorForBoundedDrain(stateManager: stateManager);

        await InvokeOnActivateAsync(ctx.Actor);

        stateManager.CommittedState["pending_command_count"].ShouldBe(2);
        stateManager.Trace.Count(operation => operation == "SaveState").ShouldBe(1);
    }

    [Fact]
    public async Task OnActivate_ReconciliationSaveFailsBeforeCommit_FirstLaterReadRepairsIt() {
        var stateManager = new FaultInjectingActorStateManager();
        var index = new UnpublishedPublicationIndex([Entry("msg-owner", "corr-owner")]);
        await stateManager.SeedCommittedStateAsync(new Dictionary<string, object> {
            [UnpublishedPublicationIndex.StateKey] = index,
            ["pending_command_count"] = 0,
            ["drain:msg-owner"] = new UnpublishedEventsRecord(
                "corr-owner", 1, 1, 1, "CreateOrder", false, DateTimeOffset.UtcNow,
                0, null, "msg-owner", ReminderArmedAt: DateTimeOffset.UtcNow),
        });
        stateManager.FaultOnCall("SaveState", 1, new InvalidOperationException("pre-commit failure"));
        ActivationContext ctx = CreateActorForBoundedDrain(stateManager: stateManager);

        await InvokeOnActivateAsync(ctx.Actor);
        stateManager.CommittedState["pending_command_count"].ShouldBe(0);

        _ = await ctx.Actor.GetStreamMetadataAsync();

        stateManager.CommittedState["pending_command_count"].ShouldBe(1);
        stateManager.Trace.Count(operation => operation == "SaveState").ShouldBe(2);
    }

    [Fact]
    public async Task OnActivate_PrecommitReconciliationFailure_NextActivationUsingSameDurableManagerRepairsIt() {
        var stateManager = new FaultInjectingActorStateManager();
        var index = new UnpublishedPublicationIndex([Entry("msg-owner", "corr-owner")]);
        await stateManager.SeedCommittedStateAsync(new Dictionary<string, object> {
            [UnpublishedPublicationIndex.StateKey] = index,
            ["pending_command_count"] = 0,
            ["drain:msg-owner"] = new UnpublishedEventsRecord(
                "corr-owner", 1, 1, 1, "CreateOrder", false, DateTimeOffset.UtcNow,
                0, null, "msg-owner", ReminderArmedAt: DateTimeOffset.UtcNow),
        });
        stateManager.FaultOnCall("SaveState", 1, new InvalidOperationException("pre-commit failure"));
        ActivationContext first = CreateActorForBoundedDrain(stateManager: stateManager);

        await InvokeOnActivateAsync(first.Actor);
        stateManager.CommittedState["pending_command_count"].ShouldBe(0);

        ActivationContext next = CreateActorForBoundedDrain(stateManager: stateManager);
        await InvokeOnActivateAsync(next.Actor);

        stateManager.CommittedState["pending_command_count"].ShouldBe(1);
    }

    [Fact]
    public async Task OnActivate_ReminderStampCommitsThenThrows_ObservesStampWithoutSecondSave() {
        var stateManager = new FaultInjectingActorStateManager();
        UnpublishedEventsRecord record = new(
            "corr-owner", 1, 1, 1, "CreateOrder", false, DateTimeOffset.UtcNow,
            0, null, "msg-owner");
        await stateManager.SeedCommittedStateAsync(new Dictionary<string, object> {
            [UnpublishedPublicationIndex.StateKey] = new UnpublishedPublicationIndex([
                Entry("msg-owner", "corr-owner"),
            ]),
            ["pending_command_count"] = 1,
            ["drain:msg-owner"] = record,
        });
        stateManager.FaultAfterCall("SaveState", 1, new InvalidOperationException("commit uncertain"));
        ActivationContext ctx = CreateActorForBoundedDrain(stateManager: stateManager);

        await InvokeOnActivateAsync(ctx.Actor);

        ((UnpublishedEventsRecord)stateManager.CommittedState["drain:msg-owner"])
            .ReminderArmedAt.ShouldNotBeNull();
        stateManager.Trace.Count(operation => operation == "SaveState").ShouldBe(1);
    }

    [Fact]
    public async Task OnActivate_ReminderStampFailsBeforeCommit_LeavesRecordUnstampedWithoutCachedMutation() {
        var stateManager = new FaultInjectingActorStateManager();
        UnpublishedEventsRecord record = new(
            "corr-owner", 1, 1, 1, "CreateOrder", false, DateTimeOffset.UtcNow,
            0, null, "msg-owner");
        await stateManager.SeedCommittedStateAsync(new Dictionary<string, object> {
            [UnpublishedPublicationIndex.StateKey] = new UnpublishedPublicationIndex([
                Entry("msg-owner", "corr-owner"),
            ]),
            ["pending_command_count"] = 1,
            ["drain:msg-owner"] = record,
        });
        stateManager.FaultOnCall("SaveState", 1, new InvalidOperationException("pre-commit failure"));
        ActivationContext ctx = CreateActorForBoundedDrain(stateManager: stateManager);

        await InvokeOnActivateAsync(ctx.Actor);

        ((UnpublishedEventsRecord)stateManager.CommittedState["drain:msg-owner"])
            .ReminderArmedAt.ShouldBeNull();
        stateManager.Trace.Count(operation => operation == "SaveState").ShouldBe(1);
        stateManager.Trace.ShouldContain("ClearCache");
    }

    [Fact]
    public async Task OnActivate_IndexReadFails_DegradesToNoOpWithoutFailingActivation() {
        ActivationContext ctx = CreateActorForBoundedDrain();
        _ = ctx.StateManager.TryGetStateAsync<UnpublishedPublicationIndex>(
            UnpublishedPublicationIndex.StateKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ConditionalValue<UnpublishedPublicationIndex>>(
                new InvalidOperationException("state store unavailable")));

        await Should.NotThrowAsync(() => InvokeOnActivateAsync(ctx.Actor));

        await ctx.StateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
        await ctx.TimerManager.DidNotReceive().RegisterReminderAsync(Arg.Any<ActorReminder>());
    }

    // ===== Bounded per-activation work =====

    [Fact]
    public async Task OnActivate_MoreEntriesThanTheActivationBudget_RearmsOnlyTheBudgetAndDefersTheRest() {
        const int entryCount = 12;
        const int activationBudget = 8;
        ActivationContext ctx = CreateActorForBoundedDrain();
        UnpublishedPublicationEntry[] entries = [.. Enumerable.Range(0, entryCount)
            .Select(i => Entry($"msg-{i:D2}", $"corr-{i:D2}"))];
        SeedPublicationIndex(ctx.StateManager, entries);
        foreach (UnpublishedPublicationEntry entry in entries) {
            SeedDrainRecord(ctx.StateManager, entry.MessageId, new UnpublishedEventsRecord(
                entry.CorrelationId, 1, 1, 1, "CreateOrder", false, DateTimeOffset.UtcNow,
                RetryCount: 0, LastFailureReason: null, MessageId: entry.MessageId));
        }

        await InvokeOnActivateAsync(ctx.Actor);

        await ctx.TimerManager.Received(activationBudget).RegisterReminderAsync(Arg.Any<ActorReminder>());
        for (int i = 0; i < activationBudget; i++) {
            string expected = $"drain-unpublished-msg-{i:D2}";
            await ctx.TimerManager.Received(1).RegisterReminderAsync(
                Arg.Is<ActorReminder>(r => r.Name == expected));
        }

        for (int i = activationBudget; i < entryCount; i++) {
            string deferred = $"drain-unpublished-msg-{i:D2}";
            await ctx.TimerManager.DidNotReceive().RegisterReminderAsync(
                Arg.Is<ActorReminder>(r => r.Name == deferred));
        }
    }

    // ===== Per-creation-site index staging (never seeds the index) =====

    [Fact]
    public async Task ProcessCommand_PublishFails_StagesIndexEntryForTheFirstPassCreationSite() {
        ActivationContext ctx = CreateActorForBoundedDrain();
        ConfigureCommandDefaults(ctx);
        CommandEnvelope envelope = CreateEnvelope("msg-first-pass", "corr-first-pass");
        _ = ctx.EventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));

        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        await ctx.StateManager.Received().SetStateAsync(
            "drain:msg-first-pass",
            Arg.Any<UnpublishedEventsRecord>(),
            Arg.Any<CancellationToken>());
        await ctx.StateManager.Received().SetStateAsync(
            UnpublishedPublicationIndex.StateKey,
            Arg.Is<UnpublishedPublicationIndex>(i => i.Contains("msg-first-pass")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommand_StaleCommittedCheckpointHandoff_StagesIndexEntryForTheHandoffCreationSite() {
        ActivationContext ctx = CreateActorForBoundedDrain();
        ConfigureCommandDefaults(ctx);

        // A committed checkpoint left by a DIFFERENT message under the same correlation id.
        SeedCheckpoint(ctx.StateManager, "corr-shared", new PipelineState(
            "corr-shared",
            CommandStatus.EventsStored,
            "CreateOrder",
            DateTimeOffset.UtcNow.AddMinutes(-5),
            EventCount: 2,
            RejectionEventType: null,
            MessageId: "msg-stale",
            CausationId: "msg-stale",
            StartSequence: 1,
            EndSequence: 2));

        _ = await ctx.Actor.ProcessCommandAsync(CreateEnvelope("msg-incoming", "corr-shared"));

        await ctx.StateManager.Received().SetStateAsync(
            "drain:msg-stale",
            Arg.Any<UnpublishedEventsRecord>(),
            Arg.Any<CancellationToken>());
        await ctx.StateManager.Received().SetStateAsync(
            UnpublishedPublicationIndex.StateKey,
            Arg.Is<UnpublishedPublicationIndex>(i => i.Contains("msg-stale")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommand_StaleHandoffWhenIndexAtCapacity_StillPersistsDrainRecordAndDoesNotReject() {
        // Post-commit choke point must fail open: already-committed ranges keep their drain record
        // even when the recovery index refuses a new entry. Pre-commit capacity tests must not be
        // the only coverage of StoreDrainRecordAndRegisterReminderAsync refusal.
        ActivationContext ctx = CreateActorForBoundedDrain(
            new EventDrainOptions { MaxOutstandingPublicationEntries = 1 });
        ConfigureCommandDefaults(ctx);

        _ = ctx.StateManager.TryGetStateAsync<UnpublishedPublicationIndex>(
            UnpublishedPublicationIndex.StateKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedPublicationIndex>(
                true,
                new UnpublishedPublicationIndex([
                    new UnpublishedPublicationEntry("msg-other", "corr-other", DateTimeOffset.UtcNow),
                ])));

        SeedCheckpoint(ctx.StateManager, "corr-shared", new PipelineState(
            "corr-shared",
            CommandStatus.EventsStored,
            "CreateOrder",
            DateTimeOffset.UtcNow.AddMinutes(-5),
            EventCount: 2,
            RejectionEventType: null,
            MessageId: "msg-stale",
            CausationId: "msg-stale",
            StartSequence: 1,
            EndSequence: 2));

        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(
            CreateEnvelope("msg-incoming", "corr-shared"));

        // The handoff must persist the drain even when the index is full. The incoming command may
        // still be fail-closed at its own pre-commit index staging — that is a separate path.
        _ = result;
        await ctx.StateManager.Received().SetStateAsync(
            "drain:msg-stale",
            Arg.Any<UnpublishedEventsRecord>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommand_ResumePublishFails_PreservesDeadLetteredAndReminderArmedAt() {
        ActivationContext ctx = CreateActorForBoundedDrain();
        ConfigureCommandDefaults(ctx);

        SeedCheckpoint(ctx.StateManager, "corr-resume", new PipelineState(
            "corr-resume",
            CommandStatus.EventsStored,
            "CreateOrder",
            DateTimeOffset.UtcNow.AddSeconds(-5),
            EventCount: 2,
            RejectionEventType: null,
            MessageId: "msg-resume",
            CausationId: "msg-resume",
            StartSequence: 1,
            EndSequence: 2));

        DateTimeOffset armedAt = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        SeedDrainRecord(ctx.StateManager, "msg-resume", new UnpublishedEventsRecord(
            "corr-resume", 1, 2, 2, "CreateOrder", false, DateTimeOffset.UtcNow.AddMinutes(-10),
            RetryCount: 3, LastFailureReason: "prior", MessageId: "msg-resume",
            DeadLettered: true, ReminderArmedAt: armedAt));

        _ = ctx.StateManager.TryGetStateAsync<AggregateMetadata>(
            "test-tenant:test-domain:agg-001:metadata", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(2, DateTimeOffset.UtcNow, null)));
        for (int seq = 1; seq <= 2; seq++) {
            var evt = new EventEnvelope(
                $"evt-{seq}", "agg-001", "test-aggregate", "test-tenant", "test-domain", seq, 0,
                DateTimeOffset.UtcNow, "corr-resume", $"cause-{seq}", "system", "1.0.0", "TestEvent", 1,
                "json", [1], null);
            _ = ctx.StateManager.TryGetStateAsync<EventEnvelope>(
                $"test-tenant:test-domain:agg-001:events:{seq}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, evt));
        }

        _ = ctx.EventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));

        _ = await ctx.Actor.ProcessCommandAsync(CreateEnvelope("msg-resume", "corr-resume"));

        await ctx.StateManager.Received().SetStateAsync(
            "drain:msg-resume",
            Arg.Is<UnpublishedEventsRecord>(r =>
                r.RetryCount == 3
                && r.DeadLettered
                && r.ReminderArmedAt == armedAt),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommand_ResumePublishFails_StagesIndexEntryForTheResumeCreationSite() {
        ActivationContext ctx = CreateActorForBoundedDrain();
        ConfigureCommandDefaults(ctx);

        SeedCheckpoint(ctx.StateManager, "corr-resume", new PipelineState(
            "corr-resume",
            CommandStatus.EventsStored,
            "CreateOrder",
            DateTimeOffset.UtcNow.AddSeconds(-5),
            EventCount: 2,
            RejectionEventType: null,
            MessageId: "msg-resume",
            CausationId: "msg-resume",
            StartSequence: 1,
            EndSequence: 2));

        _ = ctx.StateManager.TryGetStateAsync<AggregateMetadata>(
            "test-tenant:test-domain:agg-001:metadata", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(2, DateTimeOffset.UtcNow, null)));
        for (int seq = 1; seq <= 2; seq++) {
            var evt = new EventEnvelope(
                $"evt-{seq}", "agg-001", "test-aggregate", "test-tenant", "test-domain", seq, 0,
                DateTimeOffset.UtcNow, "corr-resume", $"cause-{seq}", "system", "1.0.0", "TestEvent", 1,
                "json", [1], null);
            _ = ctx.StateManager.TryGetStateAsync<EventEnvelope>(
                $"test-tenant:test-domain:agg-001:events:{seq}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, evt));
        }

        _ = ctx.EventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));

        _ = await ctx.Actor.ProcessCommandAsync(CreateEnvelope("msg-resume", "corr-resume"));

        await ctx.StateManager.Received().SetStateAsync(
            "drain:msg-resume",
            Arg.Any<UnpublishedEventsRecord>(),
            Arg.Any<CancellationToken>());
        await ctx.StateManager.Received().SetStateAsync(
            UnpublishedPublicationIndex.StateKey,
            Arg.Is<UnpublishedPublicationIndex>(i => i.Contains("msg-resume")),
            Arg.Any<CancellationToken>());
    }

    // ===== PR2: every arming site stamps ReminderArmedAt =====
    // A record created by a command and left unstamped reaches the next activation looking unarmed
    // despite a live reminder, so activation re-registers it: the due time resets to
    // InitialDrainDelay and one of the 8 re-arm slots is spent. With more than 8 outstanding records
    // the first activation after an outage still starves the tail -- the exact condition the
    // two-budget design exists to remove.

    [Fact]
    public async Task ProcessCommand_PublishFails_StampsReminderArmedAtForTheFirstPassCreationSite() {
        ActivationContext ctx = CreateActorForBoundedDrain();
        ConfigureCommandDefaults(ctx);
        _ = ctx.EventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));

        _ = await ctx.Actor.ProcessCommandAsync(CreateEnvelope("msg-stamp-first", "corr-stamp-first"));

        await ctx.StateManager.Received(1).SetStateAsync(
            "drain:msg-stamp-first",
            Arg.Is<UnpublishedEventsRecord>(r => r.ReminderArmedAt != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommand_StaleCommittedCheckpointHandoff_StampsReminderArmedAt() {
        ActivationContext ctx = CreateActorForBoundedDrain();
        ConfigureCommandDefaults(ctx);
        SeedCheckpoint(ctx.StateManager, "corr-shared-stamp", new PipelineState(
            "corr-shared-stamp",
            CommandStatus.EventsStored,
            "CreateOrder",
            DateTimeOffset.UtcNow.AddMinutes(-5),
            EventCount: 2,
            RejectionEventType: null,
            MessageId: "msg-stale-stamp",
            CausationId: "msg-stale-stamp",
            StartSequence: 1,
            EndSequence: 2));

        _ = await ctx.Actor.ProcessCommandAsync(CreateEnvelope("msg-incoming", "corr-shared-stamp"));

        await ctx.StateManager.Received(1).SetStateAsync(
            "drain:msg-stale-stamp",
            Arg.Is<UnpublishedEventsRecord>(r => r.ReminderArmedAt != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommand_ResumePublishFails_StampsReminderArmedAt() {
        ActivationContext ctx = CreateActorForBoundedDrain();
        ConfigureCommandDefaults(ctx);
        SeedCheckpoint(ctx.StateManager, "corr-resume-stamp", new PipelineState(
            "corr-resume-stamp",
            CommandStatus.EventsStored,
            "CreateOrder",
            DateTimeOffset.UtcNow.AddSeconds(-5),
            EventCount: 2,
            RejectionEventType: null,
            MessageId: "msg-resume-stamp",
            CausationId: "msg-resume-stamp",
            StartSequence: 1,
            EndSequence: 2));
        _ = ctx.StateManager.TryGetStateAsync<AggregateMetadata>(
            "test-tenant:test-domain:agg-001:metadata", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(2, DateTimeOffset.UtcNow, null)));
        for (int seq = 1; seq <= 2; seq++) {
            var evt = new EventEnvelope(
                $"evt-{seq}", "agg-001", "test-aggregate", "test-tenant", "test-domain", seq, 0,
                DateTimeOffset.UtcNow, "corr-resume-stamp", $"cause-{seq}", "system", "1.0.0", "TestEvent", 1,
                "json", [1], null);
            _ = ctx.StateManager.TryGetStateAsync<EventEnvelope>(
                $"test-tenant:test-domain:agg-001:events:{seq}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, evt));
        }

        _ = ctx.EventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));

        _ = await ctx.Actor.ProcessCommandAsync(CreateEnvelope("msg-resume-stamp", "corr-resume-stamp"));

        await ctx.StateManager.Received(1).SetStateAsync(
            "drain:msg-resume-stamp",
            Arg.Is<UnpublishedEventsRecord>(r => r.ReminderArmedAt != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommand_ReminderRegistrationFails_LeavesRecordUnstampedSoActivationReArms() {
        // Fail-closed direction: an unstamped record costs a redundant re-registration later; a
        // record wrongly stamped armed would be skipped by activation forever and never publish.
        ActivationContext ctx = CreateActorForBoundedDrain();
        ConfigureCommandDefaults(ctx);
        _ = ctx.EventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));
        _ = ctx.TimerManager.RegisterReminderAsync(Arg.Any<ActorReminder>())
            .Returns(Task.FromException(new InvalidOperationException("reminder store unavailable")));

        _ = await ctx.Actor.ProcessCommandAsync(CreateEnvelope("msg-unstamped", "corr-unstamped"));

        await ctx.StateManager.DidNotReceive().SetStateAsync(
            "drain:msg-unstamped",
            Arg.Is<UnpublishedEventsRecord>(r => r.ReminderArmedAt != null),
            Arg.Any<CancellationToken>());
        await ctx.StateManager.Received().SetStateAsync(
            "drain:msg-unstamped",
            Arg.Is<UnpublishedEventsRecord>(r => r.ReminderArmedAt == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommand_EventsCommittedAndPublicationSucceeds_StagesIndexEntryInTheCommitBatch() {
        // The commit-batch staging site is the ONLY one that runs on a path where publication later
        // succeeds — the choke point never executes because no drain record is created. This test
        // must therefore drive a SUCCEEDING publication and must NOT seed the index, or the
        // assertion would be satisfied by the fixture instead of by the staging call.
        ActivationContext ctx = CreateActorForBoundedDrain();
        ConfigureCommandDefaults(ctx);
        CommandEnvelope envelope = CreateEnvelope("msg-commit-batch", "corr-commit-batch");

        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        result.Accepted.ShouldBeTrue();

        // No drain record was created, so the choke point cannot have staged this entry.
        await ctx.StateManager.DidNotReceive().SetStateAsync(
            Arg.Is<string>(s => s.StartsWith("drain:", StringComparison.Ordinal)),
            Arg.Any<UnpublishedEventsRecord>(),
            Arg.Any<CancellationToken>());
        await ctx.StateManager.Received().SetStateAsync(
            UnpublishedPublicationIndex.StateKey,
            Arg.Is<UnpublishedPublicationIndex>(i => i.Contains("msg-commit-batch")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommand_PublishSucceeds_ReleasesTheIndexEntryInTheTerminalBatch() {
        ActivationContext ctx = CreateActorForBoundedDrain();
        ConfigureCommandDefaults(ctx);
        CommandEnvelope envelope = CreateEnvelope("msg-happy", "corr-happy");
        SeedPublicationIndex(ctx.StateManager, Entry("msg-happy", "corr-happy"));
        _ = ctx.EventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(callInfo => new EventPublishResult(true, callInfo.ArgAt<IReadOnlyList<EventEnvelope>>(1).Count, null));

        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        result.Accepted.ShouldBeTrue();
        await ctx.StateManager.Received().SetStateAsync(
            UnpublishedPublicationIndex.StateKey,
            Arg.Is<UnpublishedPublicationIndex>(i => !i.Contains("msg-happy")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommand_RecoverableRecordPastRetention_ReturnsRecoverableWithoutReExecutingTheDomain() {
        // Matrix row: a stored-but-unpublished command retried after the 24h window must still be
        // classified Recoverable. Failing open to Expired/Miss would re-execute the domain over an
        // aggregate that already has the events committed.
        ActivationContext ctx = CreateActorForBoundedDrain();
        ConfigureCommandDefaults(ctx);
        CommandEnvelope envelope = CreateEnvelope("msg-stale-recoverable", "corr-stale-recoverable");
        var expired = new IdempotencyRecord(
            CausationId: envelope.MessageId,
            CorrelationId: envelope.CorrelationId,
            Accepted: true,
            ErrorMessage: null,
            ProcessedAt: DateTimeOffset.UtcNow.AddDays(-3),
            EventCount: 2,
            MessageId: envelope.MessageId,
            CommandType: envelope.CommandType,
            ExpiresAt: DateTimeOffset.UtcNow.AddDays(-2),
            Disposition: IdempotencyRecordDisposition.Recoverable);
        _ = ctx.StateManager.TryGetStateAsync<IdempotencyRecord>(
            $"idempotency:{envelope.MessageId}", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(true, expired));

        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        result.Accepted.ShouldBeTrue();
        result.ErrorMessage.ShouldNotBe("idempotency_key_expired");
        result.EventCount.ShouldBe(2);
        _ = await ctx.Invoker.DidNotReceive().InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>());
        _ = await ctx.EventPublisher.DidNotReceive().PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>());
    }

    private sealed record TestEvent : Hexalith.EventStore.Contracts.Events.IEventPayload;
}
