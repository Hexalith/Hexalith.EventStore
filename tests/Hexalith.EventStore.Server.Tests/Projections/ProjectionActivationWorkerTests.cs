using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class ProjectionActivationWorkerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualGuardTail_WorkerClosureUsesThreeSourceInspectionsAndPreservesNewerActivationOrExpiredCleanup(bool newerAppend)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 1).ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        fixture.ReleaseDelay = TimeSpan.FromSeconds(29);
        var persisted = new DaprProjectionActivationOutbox(fixture.Backend, Options.Create(new ProjectionOptions()), fixture.Clock);
        await persisted.EnsureAsync(fixture.Identity).ConfigureAwait(true);
        ProjectionActivationWorkItem sampled = (await persisted.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldNotBeNull();
        var delayed = new DelayedProjectionActivationOutbox(persisted)
        {
            BeforeCompleteAsync = async token =>
            {
                if (newerAppend)
                {
                    await persisted.EnsureAsync(fixture.Identity, token).ConfigureAwait(false);
                    await fixture.AppendPersistedSourceEventAsync().ConfigureAwait(false);
                }
                else
                {
                    fixture.Clock.Advance(TimeSpan.FromSeconds(1));
                    token.ThrowIfCancellationRequested();
                }
            },
        };
        using var worker = new ProjectionActivationWorker(delayed, Substitute.For<IProjectionUpdateOrchestrator>(),
            Options.Create(new ProjectionDispatchOptions()), fixture.Clock, fixture.Probe);
        using (ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope())
        {
            await worker.RunOnceAsync(CancellationToken.None).ConfigureAwait(true);
            cost[ProjectionDeliveryCost.SourceInspections].ShouldBe(3);
            cost[ProjectionDeliveryCost.SourceReturnedEvents].ShouldBe(6);
            cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(2);
        }
        ProjectionActivationWorkItem pending = (await persisted.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldNotBeNull();
        pending.Revision.ShouldBe(sampled.Revision + (newerAppend ? 1 : 0));
        pending.Attempt.ShouldBe(0);
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(3);
        fixture.Rows.Values.Single().ActiveReservation.ShouldBeNull();
        fixture.Rows.Values.Single().IdleBindingPending.ShouldBeNull();
        fixture.Head.HeadSequence.ShouldBe(newerAppend ? 4 : 3);
        fixture.Handlers.Single().Calls.ShouldBe(2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ScheduledReconciliation_CompletesOnlyProvenCurrentSampledRevision(bool current)
    {
        var clock = new FakeTimeProvider();
        var identity = new AggregateIdentity("tenant-a", "counter", "aggregate-1");
        ProjectionActivationWorkItem work = ProjectionActivationWorkItem.Create(identity, clock.GetUtcNow()) with { Revision = 7 };
        IProjectionActivationOutbox outbox = Substitute.For<IProjectionActivationOutbox>();
        outbox.GetDueAsync(clock.GetUtcNow(), 64, Arg.Any<CancellationToken>()).Returns([work]);
        IProjectionSourceGuardDelivery guard = Substitute.For<IProjectionSourceGuardDelivery>();
        guard.ReclaimCompletedReplayAsync(identity, Arg.Any<CancellationToken>()).Returns(true);
        guard.TryDeliverAsync(identity, Arg.Any<CancellationToken>()).Returns(new ProjectionSourceGuardDeliveryResult(true, current,
            current ? null : "delivery_reconciliation_required"));
        IProjectionUpdateOrchestrator delivery = Substitute.For<IProjectionUpdateOrchestrator>();
        using var worker = new ProjectionActivationWorker(outbox, delivery, Options.Create(new ProjectionDispatchOptions()), clock, guard);
        await worker.RunOnceAsync(CancellationToken.None).ConfigureAwait(true);
        await outbox.Received(current ? 1 : 0).CompleteAsync(work, Arg.Any<CancellationToken>()).ConfigureAwait(true);
        await outbox.Received(current ? 0 : 1).DeferAsync(work, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).ConfigureAwait(true);
        await delivery.DidNotReceiveWithAnyArgs().UpdateProjectionAsync(default!, default).ConfigureAwait(true);
    }
}
