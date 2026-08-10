using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
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
    public async Task RegisterAsync_RejectsActorIdThatIsNotBoundToProtectedIdentity()
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

        exception.Message.ShouldContain("references are invalid");
    }

    [Fact]
    public async Task AdmitAsync_ActiveRegisteredReferenceRoutesInsideLifecycleTurn()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyAdmissionActor admission = Substitute.For<IIdempotencyAdmissionActor>();
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                new ActorId("tenant-a:v1:key-a"),
                IdempotencyAdmissionActor.ActorTypeName)
            .Returns(admission);
        IdempotencyAdmissionResult expected = new(IdempotencyAdmissionDecision.Execute, 3);
        _ = admission.AdmitAsync(Arg.Any<IdempotencyAdmissionRequest>()).Returns(expected);
        (IdempotencyTenantLifecycleActor actor, _, _) = CreateActor(factory);
        var reference = new IdempotencyTenantLifecycleReference("tenant-a:v1:key-a", "v1", "key-a");
        await actor.RegisterAsync([reference]);

        IdempotencyAdmissionResult result = await actor.AdmitAsync(
            new IdempotencyTenantLifecycleAdmissionRequest(reference, AdmissionRequest()));

        result.ShouldBe(expected);
        _ = await admission.Received(1).AdmitAsync(AdmissionRequest());
    }

    [Fact]
    public async Task AdmitAsync_DeletionAfterRegistrationCannotCreateAdmissionState()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        (IdempotencyTenantLifecycleActor actor, _, _) = CreateActor(factory);
        var reference = new IdempotencyTenantLifecycleReference("tenant-a:v1:key-a", "v1", "key-a");
        await actor.RegisterAsync([reference]);
        _ = await actor.EnterDeletionAsync(_now);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => actor.AdmitAsync(
                new IdempotencyTenantLifecycleAdmissionRequest(reference, AdmissionRequest())));

        _ = factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdempotencyAdmissionActor>(
            default!,
            default!);
    }

    [Fact]
    public async Task RegisterAsync_ExistingReferenceAfterDeletionIsStillDenied()
    {
        (IdempotencyTenantLifecycleActor actor, _, _) = CreateActor();
        var reference = new IdempotencyTenantLifecycleReference("tenant-a:v1:key-a", "v1", "key-a");
        await actor.RegisterAsync([reference]);
        _ = await actor.EnterDeletionAsync(_now);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => actor.RegisterAsync([reference]));

        exception.Message.ShouldContain("forbids idempotency admission");
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
    public async Task PurgeAsync_BoundedBatchesMarkPurgedOnlyAfterEveryProtectedReference()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyAdmissionActor admission = Substitute.For<IIdempotencyAdmissionActor>();
        IIdempotencyAdmissionDirectoryActor directory = Substitute.For<IIdempotencyAdmissionDirectoryActor>();
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionActor.ActorTypeName)
            .Returns(admission);
        _ = factory.CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionDirectoryActor.ActorTypeName)
            .Returns(directory);
        _ = admission.PurgeTombstoneAsync(Arg.Any<IdempotencyAdmissionPurgeRequest>()).Returns(true);
        (IdempotencyTenantLifecycleActor actor, _, FakeTimeProvider time) = CreateActor(factory);
        await actor.RegisterAsync(
        [
            new IdempotencyTenantLifecycleReference("tenant-a:v1:key-a", "v1", "key-a"),
            new IdempotencyTenantLifecycleReference("tenant-a:v1:key-b", "v1", "key-b"),
        ]);
        _ = await actor.EnterDeletionAsync(_now);
        time.Advance(TimeSpan.FromDays(400));

        IdempotencyTenantLifecycleRecord oneRemaining = await actor.PurgeAsync(1);
        IdempotencyTenantLifecycleRecord purged = await actor.PurgeAsync(1);

        oneRemaining.State.ShouldBe(IdempotencyTenantLifecycleState.PurgeEligible);
        oneRemaining.References.ShouldHaveSingleItem();
        purged.State.ShouldBe(IdempotencyTenantLifecycleState.Purged);
        purged.References.ShouldBeEmpty();
        await admission.Received(2).PurgeTombstoneAsync(Arg.Any<IdempotencyAdmissionPurgeRequest>());
        await directory.Received(2).PurgeAliasAsync(Arg.Any<IdempotencyAdmissionDirectoryAlias>());
    }

    [Fact]
    public async Task PurgeAsync_EligibleStateDeletesAndAcknowledgesInsideLifecycleTurn()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyAdmissionActor admission = Substitute.For<IIdempotencyAdmissionActor>();
        IIdempotencyAdmissionDirectoryActor directory = Substitute.For<IIdempotencyAdmissionDirectoryActor>();
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionActor.ActorTypeName)
            .Returns(admission);
        _ = factory.CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionDirectoryActor.ActorTypeName)
            .Returns(directory);
        _ = admission.PurgeTombstoneAsync(Arg.Any<IdempotencyAdmissionPurgeRequest>()).Returns(true);
        (IdempotencyTenantLifecycleActor actor, _, FakeTimeProvider time) = CreateActor(factory);
        await actor.RegisterAsync(
        [
            new IdempotencyTenantLifecycleReference("tenant-a:v1:key-a", "v1", "key-a"),
        ]);
        _ = await actor.EnterDeletionAsync(_now);
        time.Advance(TimeSpan.FromDays(400));

        IdempotencyTenantLifecycleRecord result = await actor.PurgeAsync(1);

        result.State.ShouldBe(IdempotencyTenantLifecycleState.Purged);
        result.References.ShouldBeEmpty();
        await admission.Received(1).PurgeTombstoneAsync(
            new IdempotencyAdmissionPurgeRequest("tenant-a", "v1", "key-a"));
        await directory.Received(1).PurgeAliasAsync(
            new IdempotencyAdmissionDirectoryAlias("v1", "tenant-a:v1:key-a", "key-a"));
    }

    [Fact]
    public async Task PurgeAsync_LegalHoldWinsSerializedEligibilityAndDeletesNothing()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        (IdempotencyTenantLifecycleActor actor, _, FakeTimeProvider time) = CreateActor(factory);
        await actor.RegisterAsync(
        [
            new IdempotencyTenantLifecycleReference("tenant-a:v1:key-a", "v1", "key-a"),
        ]);
        _ = await actor.EnterDeletionAsync(_now);
        time.Advance(TimeSpan.FromDays(400));
        _ = await actor.PlaceLegalHoldAsync(time.GetUtcNow());

        _ = await Should.ThrowAsync<InvalidOperationException>(() => actor.PurgeAsync(1));

        _ = factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdempotencyAdmissionActor>(
            default!,
            default!);
        _ = factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(
            default!,
            default!);
    }

    [Fact]
    public async Task PurgeAsync_LiveAdmissionRetainsGovernedReferenceAndDirectoryAlias()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyAdmissionActor admission = Substitute.For<IIdempotencyAdmissionActor>();
        IIdempotencyAdmissionDirectoryActor directory = Substitute.For<IIdempotencyAdmissionDirectoryActor>();
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionActor.ActorTypeName)
            .Returns(admission);
        _ = factory.CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionDirectoryActor.ActorTypeName)
            .Returns(directory);
        _ = admission.PurgeTombstoneAsync(Arg.Any<IdempotencyAdmissionPurgeRequest>()).Returns(false);
        (IdempotencyTenantLifecycleActor actor, _, FakeTimeProvider time) = CreateActor(factory);
        await actor.RegisterAsync(
        [
            new IdempotencyTenantLifecycleReference("tenant-a:v1:key-a", "v1", "key-a"),
        ]);
        _ = await actor.EnterDeletionAsync(_now);
        time.Advance(TimeSpan.FromDays(400));

        IdempotencyTenantLifecycleRecord result = await actor.PurgeAsync(1);

        result.State.ShouldBe(IdempotencyTenantLifecycleState.PurgeEligible);
        result.References.ShouldHaveSingleItem();
        await directory.DidNotReceive().PurgeAliasAsync(Arg.Any<IdempotencyAdmissionDirectoryAlias>());
    }

    [Fact]
    public async Task PurgeAsync_CorruptLifecycleDeletesNothing()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        (IdempotencyTenantLifecycleActor actor, IActorStateManager stateManager, _) = CreateActor(factory);
        var corrupt = new IdempotencyTenantLifecycleRecord(
            IdempotencyTenantLifecycleRecord.CurrentSchemaVersion,
            "tenant-a",
            IdempotencyTenantLifecycleState.Active,
            _now,
            _now,
            null,
            null,
            null,
            []);
        _ = stateManager.TryGetStateAsync<IdempotencyTenantLifecycleRecord>(
                IdempotencyTenantLifecycleActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyTenantLifecycleRecord>(true, corrupt));

        _ = await Should.ThrowAsync<InvalidOperationException>(() => actor.PurgeAsync(1));

        _ = factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdempotencyAdmissionActor>(
            default!,
            default!);
    }

    [Fact]
    public async Task PurgeAsync_ContradictoryEligibleRemainderOrFutureDeadlineDeletesNothing()
    {
        IdempotencyTenantLifecycleRecord valid = PurgeEligibleRecord();
        IdempotencyTenantLifecycleRecord[] corruptVariants =
        [
            valid with { RemainingRetention = TimeSpan.FromDays(1) },
            valid with { DeleteAfter = _now.AddDays(1) },
        ];

        foreach (IdempotencyTenantLifecycleRecord corrupt in corruptVariants)
        {
            IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
            (IdempotencyTenantLifecycleActor actor, IActorStateManager stateManager, _) = CreateActor(factory);
            _ = stateManager.TryGetStateAsync<IdempotencyTenantLifecycleRecord>(
                    IdempotencyTenantLifecycleActor.StateName,
                    Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<IdempotencyTenantLifecycleRecord>(true, corrupt));

            _ = await Should.ThrowAsync<InvalidOperationException>(() => actor.PurgeAsync(1));

            _ = factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdempotencyAdmissionActor>(
                default!,
                default!);
            _ = factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(
                default!,
                default!);
        }
    }

    [Fact]
    public async Task PurgeAsync_UnboundReferenceDeletesNothing()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        (IdempotencyTenantLifecycleActor actor, IActorStateManager stateManager, _) = CreateActor(factory);
        IdempotencyTenantLifecycleRecord corrupt = PurgeEligibleRecord() with
        {
            References =
            [
                new IdempotencyTenantLifecycleReference("tenant-b:v1:key-a", "v1", "key-a"),
            ],
        };
        _ = stateManager.TryGetStateAsync<IdempotencyTenantLifecycleRecord>(
                IdempotencyTenantLifecycleActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyTenantLifecycleRecord>(true, corrupt));

        _ = await Should.ThrowAsync<InvalidOperationException>(() => actor.PurgeAsync(1));

        _ = factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdempotencyAdmissionActor>(
            default!,
            default!);
    }

    [Fact]
    public async Task PurgeAsync_OversizedActorTurnIsRejectedBeforeDeletion()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        (IdempotencyTenantLifecycleActor actor, _, _) = CreateActor(factory);

        _ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => actor.PurgeAsync(IdempotencyTenantLifecycleActor.MaximumReferencesPerPurgeTurn + 1));

        _ = factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdempotencyAdmissionActor>(
            default!,
            default!);
    }

    [Fact]
    public async Task AcknowledgePurgeAsync_DirectCallerCannotBypassSerializedDeletion()
    {
        (IdempotencyTenantLifecycleActor actor, IActorStateManager stateManager, _) = CreateActor();

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => actor.AcknowledgePurgeAsync("tenant-a:v1:key-a"));

        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
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

    private static (IdempotencyTenantLifecycleActor Actor, IActorStateManager StateManager, FakeTimeProvider Time) CreateActor(
        IActorProxyFactory? actorProxyFactory = null)
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
            time,
            actorProxyFactory);
        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);
        return (actor, stateManager, time);
    }

    private static IdempotencyAdmissionRequest AdmissionRequest()
        => new(
            IdempotencyAdmissionRecord.CurrentSchemaVersion,
            "tenant-a",
            "v1",
            "key-a",
            "verification-tag",
            "intent-digest",
            IdempotencyReplayRetentionTier.Mutation,
            "01J00000000000000000000000",
            "trace-a");

    private static IdempotencyTenantLifecycleRecord PurgeEligibleRecord()
        => new(
            IdempotencyTenantLifecycleRecord.CurrentSchemaVersion,
            "tenant-a",
            IdempotencyTenantLifecycleState.PurgeEligible,
            _now,
            _now.AddDays(-401),
            _now.AddDays(-1),
            TimeSpan.Zero,
            null,
            [new IdempotencyTenantLifecycleReference("tenant-a:v1:key-a", "v1", "key-a")]);
}
