using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class ProjectionRebuildProductionPathTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrdinaryDelivery_OlderSourceBeforeActivationUsesReleasedWriterAndOwningScopedCheckpoint(bool productionNamedCoordinator)
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(2, writerActive: false).ConfigureAwait(true);
        fixture.UseOlderSource();
        if (productionNamedCoordinator) fixture.UseProductionNamedCoordinator(hasNamedRoute: false);
        await fixture.DeliverAsync().ConfigureAwait(true);
        fixture.ReleasedWrites.ShouldBe(1);
        (await fixture.Model.ReadProjectionStateAsync().ConfigureAwait(true))!.GetState().GetProperty("count").GetInt32().ShouldBe(2);
        (await ((IProjectionDeliveryCheckpointStore)fixture.Checkpoints).ReadDeliveredSequenceAsync(fixture.Identity, "counter")
            .ConfigureAwait(true)).ShouldBe(2);
        (await fixture.Rows.ReadWriterProtocolAsync().ConfigureAwait(true)).ShouldBeNull();
        (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldBeNull();
        (await fixture.LifecycleActor.ReadSnapshotAsync().ConfigureAwait(true)).Phase.ShouldBe(ProjectionLifecyclePhase.Idle);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrdinaryDelivery_OlderSourceSettlesActivationOnlyAfterNamedWorkIsDurable(bool schedulingFails)
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(2, writerActive: false).ConfigureAwait(true);
        fixture.UseOlderSource();
        if (schedulingFails)
        {
            fixture.NamedCoordinator = Substitute.For<INamedProjectionDispatchCoordinator>();
            fixture.NamedCoordinator.TryDispatchAsync(fixture.Identity, Arg.Any<DomainServiceRegistration>(),
                Arg.Any<EventEnvelope[]>(), Arg.Any<ProjectionEventDto[]>(), Arg.Any<CancellationToken>())
                .Returns<Task<bool>>(_ => throw new IOException("synthetic scheduling failure"));
        }
        else fixture.UseProductionNamedCoordinator(hasNamedRoute: true);
        await fixture.DeliverAsync().ConfigureAwait(true);
        fixture.ReleasedWrites.ShouldBe(1);
        (await fixture.Model.ReadProjectionStateAsync().ConfigureAwait(true))!.GetState().GetProperty("count").GetInt32().ShouldBe(2);
        ProjectionActivationWorkItem? activation = await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true);
        if (schedulingFails) activation.ShouldNotBeNull().LegacyDelivery.ShouldBeNull();
        else
        {
            activation.ShouldBeNull();
            ProjectionDeliveryRetryWorkItem work = (await fixture.NamedScheduler!.GetDueAsync(
                fixture.Clock.GetUtcNow().AddDays(1), 8, CancellationToken.None).ConfigureAwait(true)).ShouldHaveSingleItem();
            work.HeadMessageId.ShouldBe(fixture.Events[^1].MessageId);
            work.PendingRoutes.ShouldBe(["counter-named"]);
        }
    }

    [Theory]
    [InlineData("writer")]
    [InlineData("transient")]
    [InlineData("intent")]
    [InlineData("guarded")]
    [InlineData("guarded-under-lease")]
    [InlineData("writer-under-lease")]
    [InlineData("newer-under-lease")]
    public async Task OrdinaryDelivery_OlderSourceCannotDowngradeOwnedOrChangedWork(string boundary)
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(2, writerActive: boundary == "writer").ConfigureAwait(true);
        fixture.UseOlderSource(boundary == "transient" ? new IOException("synthetic transport failure") : null);
        ProjectionActivationWorkItem original = (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true))!;
        if (boundary == "intent")
            await ((IProjectionLegacyDeliveryOutbox)fixture.Outbox).TryBindLegacyDeliveryAsync(original,
                new("original-work", "sample", "v1", 2, fixture.Events[^1].MessageId), CancellationToken.None).ConfigureAwait(true);
        async Task GuardAsync()
        {
            ProjectionDeliveryState guarded = ProjectionDeliveryState.CreateEmpty(fixture.Identity, "counter",
                ProjectionDeliveryFingerprint.ComputeInitial(fixture.Identity, "counter"), fixture.Clock.GetUtcNow()) with
            { SchemaVersion = ProjectionDeliveryState.GuardSchemaVersion, GuardEvidenceVersion = 1 };
            (await fixture.Rows.TrySaveAsync(fixture.Identity, "counter", guarded, string.Empty).ConfigureAwait(false)).ShouldBeTrue();
        }
        if (boundary == "guarded") await GuardAsync().ConfigureAwait(true);
        if (boundary == "guarded-under-lease") fixture.BeforeLease = GuardAsync;
        if (boundary == "writer-under-lease") fixture.BeforeLease = async () =>
            (await fixture.Rows.TryActivateWriterProtocolAsync(new(1, 2, new string('a', 40), fixture.Clock.GetUtcNow()))
                .ConfigureAwait(false)).ShouldBeTrue();
        if (boundary == "newer-under-lease") fixture.BeforeLease = () => fixture.Outbox.EnsureAsync(fixture.Identity);
        await fixture.DeliverAsync().ConfigureAwait(true);
        fixture.ReleasedWrites.ShouldBe(0);
        (await fixture.Model.ReadProjectionStateAsync().ConfigureAwait(true)).ShouldBeNull();
        ProjectionActivationWorkItem retained = (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldNotBeNull();
        if (boundary == "intent") retained.LegacyDelivery!.OperationId.ShouldBe("original-work");
        if (boundary == "newer-under-lease") retained.Revision.ShouldBeGreaterThan(original.Revision);
        if (boundary is "writer" or "transient" or "intent") fixture.Http.Preparations.ShouldBeEmpty();
        (await fixture.LifecycleActor.ReadSnapshotAsync().ConfigureAwait(true)).Phase.ShouldBe(ProjectionLifecyclePhase.Idle);
    }

    [Fact]
    public async Task OrdinaryDelivery_OlderPublicationAfterIntentRetainsOriginalWithoutReleasedWrite()
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(2, writerActive: false).ConfigureAwait(true);
        fixture.PublicationMethodUnavailable = true;
        await fixture.DeliverAsync().ConfigureAwait(true);
        ProjectionLegacyDeliveryIntent original = (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true))!.LegacyDelivery.ShouldNotBeNull();
        original.ProjectionType.ShouldBe("counter");
        fixture.Clock.Advance(TimeSpan.FromMinutes(6));
        await fixture.DeliverAsync().ConfigureAwait(true);
        (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true))!.LegacyDelivery!.OperationId.ShouldBe(original.OperationId);
        fixture.ReleasedWrites.ShouldBe(0);
        (await fixture.Model.ReadProjectionStateAsync().ConfigureAwait(true)).ShouldBeNull();
        (await fixture.Rows.ReadAsync(fixture.Identity, "counter").ConfigureAwait(true)).State.ShouldBeNull();
    }
}
