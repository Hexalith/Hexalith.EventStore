using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class IdempotencyTenantLifecycleActorTests
{
    private static readonly DateTimeOffset _now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EnterDeletionAsync_UsesExactFourHundredDayBoundary()
    {
        (IdempotencyTenantLifecycleActor actor, _, _) = CreateActor();

        IdempotencyTenantLifecycleRecord record = await actor.EnterDeletionAsync(_now);

        record.State.ShouldBe(IdempotencyTenantLifecycleState.Retaining);
        record.DeletionApprovedAt.ShouldBe(_now);
        record.DeleteAfter.ShouldBe(_now.AddDays(400));
        record.RemainingRetention.ShouldBe(TimeSpan.FromDays(400));
    }

    [Fact]
    public async Task EnterDeletionAsync_LateObservationDoesNotRestartApprovedRetentionInterval()
    {
        (IdempotencyTenantLifecycleActor actor, _, _) = CreateActor();
        DateTimeOffset approvedAt = _now.AddDays(-401);

        IdempotencyTenantLifecycleRecord record = await actor.EnterDeletionAsync(approvedAt);

        record.State.ShouldBe(IdempotencyTenantLifecycleState.PurgeEligible);
        record.DeletionApprovedAt.ShouldBe(approvedAt);
        record.DeleteAfter.ShouldBe(approvedAt.AddDays(400));
        record.RemainingRetention.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task RegisterAsync_RejectsConflictingProtectedIdentityForSameActorReference()
    {
        (IdempotencyTenantLifecycleActor actor, _, _) = CreateActor();
        await actor.RegisterAsync(
        [
            new IdempotencyTenantLifecycleReference("tenant-a:v1:key-a", "v1", "key-a"),
        ]);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => actor.RegisterAsync(
            [
                new IdempotencyTenantLifecycleReference("tenant-a:v1:key-a", "v2", "key-b"),
            ]));

        exception.Message.ShouldContain("conflicting protected identity");
    }

    [Fact]
    public async Task LegalHold_PausesAndResumesRemainingIntervalAtInclusiveBoundary()
    {
        (IdempotencyTenantLifecycleActor actor, _, FakeTimeProvider time) = CreateActor();
        _ = await actor.EnterDeletionAsync(_now);
        time.Advance(TimeSpan.FromDays(100));

        IdempotencyTenantLifecycleRecord held = await actor.PlaceLegalHoldAsync(time.GetUtcNow());
        time.Advance(TimeSpan.FromDays(50));
        IdempotencyTenantLifecycleRecord stillHeld = await actor.GetAsync();
        IdempotencyTenantLifecycleRecord resumed = await actor.ReleaseLegalHoldAsync(time.GetUtcNow());
        time.Advance(TimeSpan.FromDays(300));
        IdempotencyTenantLifecycleRecord eligible = await actor.GetAsync();

        held.RemainingRetention.ShouldBe(TimeSpan.FromDays(300));
        stillHeld.State.ShouldBe(IdempotencyTenantLifecycleState.LegalHold);
        stillHeld.RemainingRetention.ShouldBe(TimeSpan.FromDays(300));
        resumed.DeleteAfter.ShouldBe(_now.AddDays(450));
        eligible.State.ShouldBe(IdempotencyTenantLifecycleState.PurgeEligible);
    }

    [Fact]
    public async Task AcknowledgePurgeAsync_MarksPurgedOnlyAfterEveryProtectedReference()
    {
        (IdempotencyTenantLifecycleActor actor, _, FakeTimeProvider time) = CreateActor();
        await actor.RegisterAsync(
        [
            new IdempotencyTenantLifecycleReference("tenant-a:v1:key-a", "v1", "key-a"),
            new IdempotencyTenantLifecycleReference("tenant-a:v1:key-b", "v1", "key-b"),
        ]);
        _ = await actor.EnterDeletionAsync(_now);
        time.Advance(TimeSpan.FromDays(400));
        _ = await actor.GetAsync();

        IdempotencyTenantLifecycleRecord oneRemaining = await actor.AcknowledgePurgeAsync("tenant-a:v1:key-a");
        IdempotencyTenantLifecycleRecord purged = await actor.AcknowledgePurgeAsync("tenant-a:v1:key-b");

        oneRemaining.State.ShouldBe(IdempotencyTenantLifecycleState.PurgeEligible);
        oneRemaining.References.ShouldHaveSingleItem();
        purged.State.ShouldBe(IdempotencyTenantLifecycleState.Purged);
        purged.References.ShouldBeEmpty();
    }

    [Fact]
    public async Task PurgeTombstoneAsync_RemovesOnlyExactCompactedStateAndPromotionMetadata()
    {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ActorHost host = ActorHost.CreateForTest<IdempotencyAdmissionActor>(
            new ActorTestOptions { ActorId = new ActorId("tenant-a:v1:key-a") });
        var actor = new IdempotencyAdmissionActor(
            host,
            NullLogger<IdempotencyAdmissionActor>.Instance,
            new FakeTimeProvider(_now));
        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);
        var tombstone = new IdempotencyAdmissionTombstone(
            IdempotencyAdmissionTombstone.CurrentSchemaVersion,
            IdempotencyAdmissionState.Expired,
            "tenant-a",
            "key-a",
            "tag-a",
            "v1",
            Hexalith.EventStore.Contracts.Commands.IdempotencyReplayRetentionTier.Mutation,
            _now.AddDays(-2),
            _now.AddDays(-1),
            _now);
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(false, default!));
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionTombstone>(
                IdempotencyAdmissionActor.TombstoneStateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionTombstone>(true, tombstone));

        bool purged = await actor.PurgeTombstoneAsync(
            new IdempotencyAdmissionPurgeRequest("tenant-a", "v1", "key-a"));

        purged.ShouldBeTrue();
        _ = await stateManager.Received(1).TryRemoveStateAsync(
            IdempotencyAdmissionActor.TombstoneStateName,
            Arg.Any<CancellationToken>());
        _ = await stateManager.Received(1).TryRemoveStateAsync(
            IdempotencyAdmissionActor.RedirectStateName,
            Arg.Any<CancellationToken>());
        _ = await stateManager.Received(1).TryRemoveStateAsync(
            IdempotencyAdmissionActor.PromotionStateName,
            Arg.Any<CancellationToken>());
        await stateManager.Received(1).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    private static (IdempotencyTenantLifecycleActor Actor, IActorStateManager StateManager, FakeTimeProvider Time) CreateActor()
    {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        var time = new FakeTimeProvider(_now);
        IdempotencyTenantLifecycleRecord? stored = null;
        _ = stateManager.TryGetStateAsync<IdempotencyTenantLifecycleRecord>(
                IdempotencyTenantLifecycleActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(_ => stored is null
                ? new ConditionalValue<IdempotencyTenantLifecycleRecord>(false, default!)
                : new ConditionalValue<IdempotencyTenantLifecycleRecord>(true, stored));
        _ = stateManager.SetStateAsync(
                IdempotencyTenantLifecycleActor.StateName,
                Arg.Do<IdempotencyTenantLifecycleRecord>(record => stored = record),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        ActorHost host = ActorHost.CreateForTest<IdempotencyTenantLifecycleActor>(
            new ActorTestOptions { ActorId = new ActorId("tenant-a") });
        var actor = new IdempotencyTenantLifecycleActor(
            host,
            NullLogger<IdempotencyTenantLifecycleActor>.Instance,
            time);
        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);
        return (actor, stateManager, time);
    }
}
