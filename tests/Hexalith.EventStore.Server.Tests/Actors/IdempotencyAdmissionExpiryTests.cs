using System.Reflection;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class IdempotencyAdmissionExpiryTests
{
    private static readonly DateTimeOffset _now = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TombstoneSchema_ContainsExactlyTheApprovedFenceFreeMetadata()
    {
        string[] actual = typeof(IdempotencyAdmissionTombstone)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(static property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] expected =
        [
            nameof(IdempotencyAdmissionTombstone.DigestKeyVersion),
            nameof(IdempotencyAdmissionTombstone.FirstConsumedAt),
            nameof(IdempotencyAdmissionTombstone.KeyDigest),
            nameof(IdempotencyAdmissionTombstone.LastObservedAt),
            nameof(IdempotencyAdmissionTombstone.ReplayExpiredAt),
            nameof(IdempotencyAdmissionTombstone.RetentionTier),
            nameof(IdempotencyAdmissionTombstone.SchemaVersion),
            nameof(IdempotencyAdmissionTombstone.State),
            nameof(IdempotencyAdmissionTombstone.TenantPartition),
            nameof(IdempotencyAdmissionTombstone.VerificationTag),
        ];
        Array.Sort(expected, StringComparer.Ordinal);

        actual.ShouldBe(expected);
    }

    [Fact]
    public async Task CompleteAsync_MutationTierArmsReminderBeforePersistingExactFinalization()
    {
        TestContext context = CreateActor();
        IdempotencyAdmissionRecord pending = Record(IdempotencyAdmissionState.Pending);
        IdempotencyAdmissionRecord? terminal = null;
        ActorReminder? reminder = null;
        _ = context.StateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(true, pending));
        _ = context.TimerManager.RegisterReminderAsync(Arg.Do<ActorReminder>(value => reminder = value))
            .Returns(Task.CompletedTask);
        _ = context.StateManager.SetStateAsync(
                IdempotencyAdmissionActor.StateName,
                Arg.Do<IdempotencyAdmissionRecord>(value => terminal = value),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await context.Actor.CompleteAsync(
            new IdempotencyAdmissionCompletionRequest(
                pending.FencingToken,
                new CommandProcessingResult(true, ResultPayload: "protected-result")));

        terminal.ShouldNotBeNull().ReplayExpiresAt.ShouldBe(_now.AddSeconds(86_400));
        reminder.ShouldNotBeNull().Name.ShouldBe(IdempotencyAdmissionActor.CompactionReminderName);
        reminder.DueTime.ShouldBe(TimeSpan.FromSeconds(86_400));
        reminder.Period.ShouldBe(TimeSpan.FromHours(1));
        Received.InOrder(() =>
        {
            _ = context.TimerManager.RegisterReminderAsync(Arg.Any<ActorReminder>());
            _ = context.StateManager.SetStateAsync(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<IdempotencyAdmissionRecord>(),
                Arg.Any<CancellationToken>());
            _ = context.StateManager.SaveStateAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task CompleteAsync_CommitTierArmsReminderBeforePersistingExactCalendarRetention()
    {
        TestContext context = CreateActor();
        IdempotencyAdmissionRecord pending = Record(IdempotencyAdmissionState.Pending) with
        {
            RetentionTier = IdempotencyReplayRetentionTier.Commit,
        };
        IdempotencyAdmissionRecord? terminal = null;
        ActorReminder? reminder = null;
        _ = context.StateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(true, pending));
        _ = context.TimerManager.RegisterReminderAsync(Arg.Do<ActorReminder>(value => reminder = value))
            .Returns(Task.CompletedTask);
        _ = context.StateManager.SetStateAsync(
                IdempotencyAdmissionActor.StateName,
                Arg.Do<IdempotencyAdmissionRecord>(value => terminal = value),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await context.Actor.CompleteAsync(
            new IdempotencyAdmissionCompletionRequest(pending.FencingToken, new CommandProcessingResult(true)));

        terminal.ShouldNotBeNull().ReplayExpiresAt.ShouldBe(_now.AddYears(7));
        reminder.ShouldNotBeNull().DueTime.ShouldBe(_now.AddYears(7) - _now);
        Received.InOrder(() =>
        {
            _ = context.TimerManager.RegisterReminderAsync(Arg.Any<ActorReminder>());
            _ = context.StateManager.SetStateAsync(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<IdempotencyAdmissionRecord>(),
                Arg.Any<CancellationToken>());
            _ = context.StateManager.SaveStateAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task CompleteAsync_ReminderFailureLeavesPendingStateUnchanged()
    {
        TestContext context = CreateActor();
        IdempotencyAdmissionRecord pending = Record(IdempotencyAdmissionState.Pending);
        _ = context.StateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(true, pending));
        _ = context.TimerManager.RegisterReminderAsync(Arg.Any<ActorReminder>())
            .Returns<Task>(_ => throw new InvalidOperationException("scheduler unavailable"));

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => context.Actor.CompleteAsync(
                new IdempotencyAdmissionCompletionRequest(pending.FencingToken, new CommandProcessingResult(true))));

        await context.StateManager.DidNotReceive().SetStateAsync(
            IdempotencyAdmissionActor.StateName,
            Arg.Any<IdempotencyAdmissionRecord>(),
            Arg.Any<CancellationToken>());
        await context.StateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_StateSaveFailureLeavesHarmlessReminderThatCallbackRemoves()
    {
        TestContext context = CreateActor();
        IdempotencyAdmissionRecord pending = Record(IdempotencyAdmissionState.Pending);
        _ = context.StateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(true, pending));
        _ = context.StateManager.SaveStateAsync(Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("state save unavailable"));

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => context.Actor.CompleteAsync(
                new IdempotencyAdmissionCompletionRequest(pending.FencingToken, new CommandProcessingResult(true))));

        _ = context.TimerManager.Received(1).RegisterReminderAsync(Arg.Any<ActorReminder>());
        _ = context.StateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(false, default!));
        _ = context.StateManager.TryGetStateAsync<IdempotencyAdmissionTombstone>(
                IdempotencyAdmissionActor.TombstoneStateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionTombstone>(false, default!));
        _ = context.TimerManager.UnregisterReminderAsync(Arg.Any<ActorReminderToken>())
            .Returns(Task.CompletedTask);

        await context.Actor.ReceiveReminderAsync(
            IdempotencyAdmissionActor.CompactionReminderName,
            [],
            TimeSpan.Zero,
            TimeSpan.FromHours(1));

        _ = context.TimerManager.Received(1).UnregisterReminderAsync(Arg.Any<ActorReminderToken>());
    }

    [Fact]
    public async Task PreparePromotionAsync_TerminalImportArmsReminderBeforeStateBatch()
    {
        TestContext context = CreateActor();
        IdempotencyAdmissionRecord terminal = Record(
            IdempotencyAdmissionState.Terminal,
            replayExpiresAt: _now.AddHours(1),
            replayResult: new CommandProcessingResult(true));
        ConfigureState(context.StateManager, live: null, compacted: null);

        await context.Actor.PreparePromotionAsync(
            new IdempotencyAdmissionPromotionImportRequest("tenant-a:v0:source", Record: terminal));

        Received.InOrder(() =>
        {
            _ = context.TimerManager.RegisterReminderAsync(Arg.Any<ActorReminder>());
            _ = context.StateManager.SetStateAsync(
                IdempotencyAdmissionActor.StateName,
                terminal,
                Arg.Any<CancellationToken>());
            _ = context.StateManager.SaveStateAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task PreparePromotionAsync_ReminderFailureLeavesImportedStateUncommitted()
    {
        TestContext context = CreateActor();
        IdempotencyAdmissionRecord terminal = Record(
            IdempotencyAdmissionState.Terminal,
            replayExpiresAt: _now.AddHours(1),
            replayResult: new CommandProcessingResult(true));
        ConfigureState(context.StateManager, live: null, compacted: null);
        _ = context.TimerManager.RegisterReminderAsync(Arg.Any<ActorReminder>())
            .Returns<Task>(_ => throw new InvalidOperationException("scheduler unavailable"));

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => context.Actor.PreparePromotionAsync(
                new IdempotencyAdmissionPromotionImportRequest("tenant-a:v0:source", Record: terminal)));

        await context.StateManager.DidNotReceive().SetStateAsync(
            IdempotencyAdmissionActor.StateName,
            Arg.Any<IdempotencyAdmissionRecord>(),
            Arg.Any<CancellationToken>());
        await context.StateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdmitAsync_LiveDifferentTrustedTierReturnsConflictBeforeExpiry()
    {
        TestContext context = CreateActor();
        IdempotencyAdmissionRecord terminal = Record(
            IdempotencyAdmissionState.Terminal,
            replayExpiresAt: _now.AddMinutes(1),
            replayResult: new CommandProcessingResult(true));
        ConfigureState(context.StateManager, terminal, compacted: null);

        IdempotencyAdmissionResult result = await context.Actor.AdmitAsync(
            Request(IdempotencyReplayRetentionTier.Commit, "different-intent"));

        result.Decision.ShouldBe(IdempotencyAdmissionDecision.Conflict);
        result.FencingToken.ShouldBe(terminal.FencingToken);
        await context.StateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReceiveReminderAsync_AtInclusiveExpiryAtomicallyReplacesLiveState()
    {
        TestContext context = CreateActor();
        IdempotencyAdmissionRecord terminal = Record(
            IdempotencyAdmissionState.Terminal,
            replayExpiresAt: _now,
            replayResult: new CommandProcessingResult(true, ResultPayload: "protected-result"));
        IdempotencyAdmissionTombstone? tombstone = null;
        ConfigureState(context.StateManager, terminal, compacted: null);
        _ = context.StateManager.SetStateAsync(
                IdempotencyAdmissionActor.TombstoneStateName,
                Arg.Do<IdempotencyAdmissionTombstone>(value => tombstone = value),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _ = context.StateManager.TryRemoveStateAsync(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(true);
        _ = context.TimerManager.UnregisterReminderAsync(Arg.Any<ActorReminderToken>())
            .Returns(Task.CompletedTask);

        await context.Actor.ReceiveReminderAsync(
            IdempotencyAdmissionActor.CompactionReminderName,
            [],
            TimeSpan.Zero,
            TimeSpan.FromHours(1));

        tombstone.ShouldNotBeNull().ReplayExpiredAt.ShouldBe(_now);
        tombstone.LastObservedAt.ShouldBe(_now);
        tombstone.ToString().ShouldNotContain("protected-result");
        typeof(IdempotencyAdmissionTombstone).GetProperty("FencingToken").ShouldBeNull();
        Received.InOrder(() =>
        {
            _ = context.StateManager.SetStateAsync(
                IdempotencyAdmissionActor.TombstoneStateName,
                Arg.Any<IdempotencyAdmissionTombstone>(),
                Arg.Any<CancellationToken>());
            _ = context.StateManager.TryRemoveStateAsync(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>());
            _ = context.StateManager.SaveStateAsync(Arg.Any<CancellationToken>());
            _ = context.TimerManager.UnregisterReminderAsync(Arg.Any<ActorReminderToken>());
        });
    }

    [Fact]
    public async Task ReceiveReminderAsync_OneTickBeforeExpiryRearmsWithoutCompaction()
    {
        TestContext context = CreateActor(_now.AddTicks(-1));
        IdempotencyAdmissionRecord terminal = Record(
            IdempotencyAdmissionState.Terminal,
            lastObservedAt: _now.AddTicks(-1),
            replayExpiresAt: _now,
            replayResult: new CommandProcessingResult(true));
        ConfigureState(context.StateManager, terminal, compacted: null);
        ActorReminder? reminder = null;
        _ = context.TimerManager.RegisterReminderAsync(Arg.Do<ActorReminder>(value => reminder = value))
            .Returns(Task.CompletedTask);

        await context.Actor.ReceiveReminderAsync(
            IdempotencyAdmissionActor.CompactionReminderName,
            [],
            TimeSpan.Zero,
            TimeSpan.FromHours(1));

        reminder.ShouldNotBeNull().DueTime.ShouldBe(TimeSpan.FromTicks(1));
        await context.StateManager.DidNotReceive().SetStateAsync(
            IdempotencyAdmissionActor.TombstoneStateName,
            Arg.Any<IdempotencyAdmissionTombstone>(),
            Arg.Any<CancellationToken>());
        _ = context.StateManager.DidNotReceive().TryRemoveStateAsync(
            IdempotencyAdmissionActor.StateName,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdmitAsync_ExpiredLiveRecordPrecedesDifferentTrustedTierAndIntent()
    {
        TestContext context = CreateActor();
        IdempotencyAdmissionRecord terminal = Record(
            IdempotencyAdmissionState.Terminal,
            replayExpiresAt: _now,
            replayResult: new CommandProcessingResult(true));
        ConfigureState(context.StateManager, terminal, compacted: null);

        IdempotencyAdmissionResult result = await context.Actor.AdmitAsync(
            Request(IdempotencyReplayRetentionTier.Commit, "different-intent"));

        result.Decision.ShouldBe(IdempotencyAdmissionDecision.Expired);
        result.FencingToken.ShouldBe(0);
        await context.StateManager.Received(1).SetStateAsync(
            IdempotencyAdmissionActor.TombstoneStateName,
            Arg.Is<IdempotencyAdmissionTombstone>(value =>
                value.RetentionTier == IdempotencyReplayRetentionTier.Mutation),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdmitAsync_TombstoneMakesEveryTrustedTierAndIntentIndistinguishable()
    {
        TestContext context = CreateActor();
        IdempotencyAdmissionTombstone tombstone = Tombstone();
        ConfigureState(context.StateManager, live: null, tombstone);

        IdempotencyAdmissionResult mutation = await context.Actor.AdmitAsync(
            Request(IdempotencyReplayRetentionTier.Mutation, "same-intent"));
        IdempotencyAdmissionResult commit = await context.Actor.AdmitAsync(
            Request(IdempotencyReplayRetentionTier.Commit, "different-intent"));

        mutation.ShouldBe(commit);
        mutation.ShouldBe(new IdempotencyAdmissionResult(IdempotencyAdmissionDecision.Expired));
        await context.StateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAuthorityAsync_ExpiredSignedContextCompactsAndFailsClosed()
    {
        TestContext context = CreateActor();
        IdempotencyAdmissionRecord terminal = Record(
            IdempotencyAdmissionState.Terminal,
            replayExpiresAt: _now,
            replayResult: new CommandProcessingResult(true));
        ConfigureState(context.StateManager, terminal, compacted: null);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => context.Actor.ValidateAuthorityAsync(Authority()));

        exception.Message.ShouldContain("no longer current");
        await context.StateManager.Received(1).SetStateAsync(
            IdempotencyAdmissionActor.TombstoneStateName,
            Arg.Any<IdempotencyAdmissionTombstone>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAuthorityAsync_PendingAcceptsOnlyExactExecuteAuthorityWithoutMutation()
    {
        TestContext context = CreateActor();
        ConfigureState(context.StateManager, Record(IdempotencyAdmissionState.Pending), compacted: null);

        await context.Actor.ValidateAuthorityAsync(Authority());

        await context.StateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
        await context.StateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAuthorityAsync_UnknownOutcomeAcceptsOnlyExactReconciliationAuthorityWithoutMutation()
    {
        TestContext context = CreateActor();
        ConfigureState(
            context.StateManager,
            Record(IdempotencyAdmissionState.UnknownProviderOutcome),
            compacted: null);

        await context.Actor.ValidateAuthorityAsync(
            Authority() with { Purpose = IdempotencyExecutionPurpose.Reconcile });

        await context.StateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
        await context.StateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAuthorityAsync_PendingRejectsEveryIdentityOrPurposeMismatchWithoutMutation()
    {
        IdempotencyAdmissionAuthorityRequest exact = Authority();
        IdempotencyAdmissionAuthorityRequest[] mismatches =
        [
            exact with { FencingToken = exact.FencingToken + 1 },
            exact with { DigestKeyVersion = "v2" },
            exact with { ExecutionMessageId = "01J99999999999999999999999" },
            exact with { ExecutionCorrelationId = "trace-other" },
            exact with { Purpose = IdempotencyExecutionPurpose.Reconcile },
        ];

        foreach (IdempotencyAdmissionAuthorityRequest mismatch in mismatches)
        {
            TestContext context = CreateActor();
            ConfigureState(context.StateManager, Record(IdempotencyAdmissionState.Pending), compacted: null);

            _ = await Should.ThrowAsync<InvalidOperationException>(
                () => context.Actor.ValidateAuthorityAsync(mismatch));

            await context.StateManager.DidNotReceive().SetStateAsync(
                Arg.Any<string>(),
                Arg.Any<object>(),
                Arg.Any<CancellationToken>());
            await context.StateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task ValidateAuthorityAsync_UnknownOutcomeRejectsExecutePurposeWithoutMutation()
    {
        TestContext context = CreateActor();
        ConfigureState(
            context.StateManager,
            Record(IdempotencyAdmissionState.UnknownProviderOutcome),
            compacted: null);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => context.Actor.ValidateAuthorityAsync(Authority()));

        await context.StateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAuthorityAsync_RedirectedSourceRejectsBeforeStateAuthority()
    {
        TestContext context = CreateActor();
        ConfigureState(context.StateManager, Record(IdempotencyAdmissionState.Pending), compacted: null);
        _ = context.StateManager.TryGetStateAsync<IdempotencyAdmissionRedirectRecord>(
                IdempotencyAdmissionActor.RedirectStateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRedirectRecord>(
                true,
                new IdempotencyAdmissionRedirectRecord(
                    IdempotencyAdmissionRedirectRecord.CurrentSchemaVersion,
                    "tenant-a:v2:target")));

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => context.Actor.ValidateAuthorityAsync(Authority()));

        _ = context.StateManager.DidNotReceive().TryGetStateAsync<IdempotencyAdmissionRecord>(
            IdempotencyAdmissionActor.StateName,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAuthorityAsync_UnactivatedPromotionTargetRejectsBeforeStateAuthority()
    {
        TestContext context = CreateActor();
        IdempotencyAdmissionRecord record = Record(IdempotencyAdmissionState.Pending);
        ConfigureState(context.StateManager, record, compacted: null);
        string digest = IdempotencyAdmissionPromotionEvidence.Compute(record, null);
        const string SourceActorId = "tenant-a:v0:source";
        _ = context.StateManager.TryGetStateAsync<IdempotencyAdmissionPromotionRecord>(
                IdempotencyAdmissionActor.PromotionStateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionPromotionRecord>(
                true,
                new IdempotencyAdmissionPromotionRecord(
                    IdempotencyAdmissionPromotionRecord.CurrentSchemaVersion,
                    SourceActorId,
                    Activated: false,
                    IdempotencyAdmissionPromotionEvidence.BuildConventionalMigrationId(
                        SourceActorId,
                        "tenant-a:v1:key-digest"),
                    digest,
                    digest,
                    digest)));

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => context.Actor.ValidateAuthorityAsync(Authority()));

        await context.StateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActivateAsync_TerminalStateRecoversMissingCompactionSchedule()
    {
        TestContext context = CreateActor(_now.AddHours(-1));
        IdempotencyAdmissionRecord terminal = Record(
            IdempotencyAdmissionState.Terminal,
            lastObservedAt: _now.AddHours(-1),
            replayExpiresAt: _now,
            replayResult: new CommandProcessingResult(true));
        ConfigureState(context.StateManager, terminal, compacted: null);
        ActorReminder? reminder = null;
        _ = context.TimerManager.RegisterReminderAsync(Arg.Do<ActorReminder>(value => reminder = value))
            .Returns(Task.CompletedTask);

        await InvokeOnActivateAsync(context.Actor);

        reminder.ShouldNotBeNull().DueTime.ShouldBe(TimeSpan.FromHours(1));
        reminder.Name.ShouldBe(IdempotencyAdmissionActor.CompactionReminderName);
    }

    [Fact]
    public async Task ReceiveReminderAsync_CorruptDualStateDoesNotDeleteEvidence()
    {
        TestContext context = CreateActor();
        ConfigureState(context.StateManager, Record(IdempotencyAdmissionState.Terminal), Tombstone());

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => context.Actor.ReceiveReminderAsync(
                IdempotencyAdmissionActor.CompactionReminderName,
                [],
                TimeSpan.Zero,
                TimeSpan.FromHours(1)));

        _ = context.StateManager.DidNotReceive().TryRemoveStateAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await context.StateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurgeTombstoneAsync_CorruptMatchingTombstoneRetainsEveryStateEntry()
    {
        IdempotencyAdmissionTombstone valid = Tombstone();
        IdempotencyAdmissionTombstone[] corruptVariants =
        [
            valid with { SchemaVersion = valid.SchemaVersion + 1 },
            valid with { State = IdempotencyAdmissionState.Terminal },
            valid with { RetentionTier = (IdempotencyReplayRetentionTier)999 },
            valid with { ReplayExpiredAt = valid.FirstConsumedAt.AddTicks(-1) },
            valid with { LastObservedAt = valid.ReplayExpiredAt.AddTicks(-1) },
        ];

        foreach (IdempotencyAdmissionTombstone corrupt in corruptVariants)
        {
            TestContext context = CreateActor();
            ConfigureState(context.StateManager, live: null, corrupt);

            _ = await Should.ThrowAsync<InvalidOperationException>(
                () => context.Actor.PurgeTombstoneAsync(
                    new IdempotencyAdmissionPurgeRequest("tenant-a", "v1", "key-digest")));

            _ = context.StateManager.DidNotReceive().TryRemoveStateAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
            await context.StateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
        }
    }

    private static void ConfigureState(
        IActorStateManager stateManager,
        IdempotencyAdmissionRecord? live,
        IdempotencyAdmissionTombstone? compacted)
    {
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(live is null
                ? new ConditionalValue<IdempotencyAdmissionRecord>(false, default!)
                : new ConditionalValue<IdempotencyAdmissionRecord>(true, live));
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionTombstone>(
                IdempotencyAdmissionActor.TombstoneStateName,
                Arg.Any<CancellationToken>())
            .Returns(compacted is null
                ? new ConditionalValue<IdempotencyAdmissionTombstone>(false, default!)
                : new ConditionalValue<IdempotencyAdmissionTombstone>(true, compacted));
    }

    private static Task InvokeOnActivateAsync(IdempotencyAdmissionActor actor)
    {
        MethodInfo method = typeof(IdempotencyAdmissionActor)
            .GetMethod("OnActivateAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("IdempotencyAdmissionActor.OnActivateAsync was not found.");
        return (Task)method.Invoke(actor, null)!;
    }

    private static TestContext CreateActor(DateTimeOffset? now = null)
    {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        var timeProvider = new FakeTimeProvider(now ?? _now);
        ActorHost host = ActorHost.CreateForTest<IdempotencyAdmissionActor>(
            new ActorTestOptions
            {
                ActorId = new ActorId("tenant-a:v1:key-digest"),
                TimerManager = timerManager,
            });
        var actor = new IdempotencyAdmissionActor(
            host,
            NullLogger<IdempotencyAdmissionActor>.Instance,
            timeProvider);
        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);
        return new TestContext(actor, stateManager, timerManager);
    }

    private static IdempotencyAdmissionAuthorityRequest Authority()
        => new(
            7,
            "v1",
            "01J00000000000000000000000",
            "trace-original",
            IdempotencyExecutionPurpose.Execute);

    private static IdempotencyAdmissionRequest Request(
        IdempotencyReplayRetentionTier tier,
        string intentDigest)
        => new(
            IdempotencyAdmissionRecord.CurrentSchemaVersion,
            "tenant-a",
            "v1",
            "key-digest",
            "verification-tag",
            intentDigest,
            tier,
            "01J00000000000000000000000",
            "trace-original");

    private static IdempotencyAdmissionRecord Record(
        IdempotencyAdmissionState state,
        DateTimeOffset? lastObservedAt = null,
        DateTimeOffset? replayExpiresAt = null,
        CommandProcessingResult? replayResult = null)
        => new(
            IdempotencyAdmissionRecord.CurrentSchemaVersion,
            state,
            "tenant-a",
            "v1",
            "key-digest",
            "verification-tag",
            "same-intent",
            IdempotencyReplayRetentionTier.Mutation,
            _now.AddHours(-2),
            lastObservedAt ?? _now,
            replayExpiresAt ?? (state == IdempotencyAdmissionState.Terminal ? _now.AddHours(1) : null),
            7,
            replayResult,
            "01J00000000000000000000000",
            "trace-original");

    private static IdempotencyAdmissionTombstone Tombstone()
        => new(
            IdempotencyAdmissionTombstone.CurrentSchemaVersion,
            IdempotencyAdmissionState.Expired,
            "tenant-a",
            "key-digest",
            "verification-tag",
            "v1",
            IdempotencyReplayRetentionTier.Mutation,
            _now.AddDays(-2),
            _now.AddDays(-1),
            _now);

    private sealed record TestContext(
        IdempotencyAdmissionActor Actor,
        IActorStateManager StateManager,
        ActorTimerManager TimerManager);
}
