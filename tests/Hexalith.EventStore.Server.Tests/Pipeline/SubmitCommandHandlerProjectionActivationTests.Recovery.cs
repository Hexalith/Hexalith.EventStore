using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Tests.Projections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Pipeline;

public sealed partial class SubmitCommandHandlerProjectionActivationTests
{
    [Theory]
    [InlineData(false, "single-event-v1")]
    [InlineData(true, "single-event-v1")]
    [InlineData(false, "full-replay-v1")]
    [InlineData(true, "full-replay-v1")]
    public async Task PostCommitRearmSurvivesOldHeadWorkerCleanupAndFreshWorkerDeliversPersistedNewHead(bool clearBeforeCommit, string semantics)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(1, 1, handlerSemantics: semantics);
        await fixture.UseProductionOwnersAsync();
        var outbox = new DaprProjectionActivationOutbox(fixture.Backend, Options.Create(new ProjectionOptions()), fixture.Clock);
        var replay = Substitute.For<IProjectionUpdateOrchestrator>();
        using var oldWorker = new ProjectionActivationWorker(outbox, replay,
            Options.Create(new ProjectionDispatchOptions()), fixture.Clock, fixture.Probe);
        ProjectionActivationWorkItem? sampled = null;
        var router = Substitute.For<ICommandRouter>();
        router.RouteCommandAsync(Arg.Any<Server.Pipeline.Commands.SubmitCommand>(), Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            sampled = (await outbox.GetAsync(fixture.Identity)).ShouldNotBeNull();
            if (clearBeforeCommit)
            {
                // This independently scheduled real worker observes the old persisted source head.
                await oldWorker.RunOnceAsync(CancellationToken.None);
                (await outbox.GetAsync(fixture.Identity)).ShouldBeNull();
            }
            await fixture.AppendPersistedSourceEventAsync();
            return new CommandProcessingResult(true, EventCount: 1);
        });
        using (ServiceProvider requestScope = BuildProvider(fixture.Backend, outbox, router, replay, clock: fixture.Clock))
        {
            SubmitCommandHandler handler = requestScope.GetRequiredService<SubmitCommandHandler>();
            (await handler.Handle(Command(), default)).MessageId.ShouldBe(Command().MessageId);
        }

        // Reconstruct the outbox after HTTP scope disposal, using only the persisted backend.
        var recovered = new DaprProjectionActivationOutbox(fixture.Backend, Options.Create(new ProjectionOptions()), fixture.Clock);
        ProjectionActivationWorkItem pending = (await recovered.GetAsync(fixture.Identity)).ShouldNotBeNull();
        pending.Revision.ShouldBeGreaterThan(sampled.ShouldNotBeNull().Revision);
        await recovered.CompleteAsync(sampled!);
        await recovered.DeferAsync(sampled!, fixture.Clock.GetUtcNow().AddHours(1));
        (await recovered.GetAsync(fixture.Identity)).ShouldBe(pending);

        fixture.ReconstructGuardedDelivery();
        using var laterWorker = new ProjectionActivationWorker(recovered, replay,
            Options.Create(new ProjectionDispatchOptions()), fixture.Clock, fixture.Probe);
        await laterWorker.RunOnceAsync(CancellationToken.None);

        (await recovered.GetAsync(fixture.Identity)).ShouldBeNull();
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        var states = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()),
            Options.Create(new ProjectionDeliveryIdempotencyOptions()));
        ProjectionDeliveryState persisted = (await states.ReadAsync(fixture.Identity, route)).State.ShouldNotBeNull();
        persisted.LastDeliveredSequence.ShouldBe(2);
        persisted.ActiveReservation.ShouldBeNull();
        persisted.IdleBindingPending.ShouldBeNull();
        var model = await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route);
        model.Value.ShouldNotBeNull().Count.ShouldBe(2);
        if (semantics == "full-replay-v1") fixture.Handlers.Single().RebuildCalls.ShouldBe(1);
        await replay.DidNotReceiveWithAnyArgs().UpdateProjectionAsync(default!, default);
    }

    [Theory]
    [InlineData("single-event-v1")]
    [InlineData("full-replay-v1")]
    public async Task WorkerFailureRetainsDurableActivationAndReconstructedWorkerRecoversPersistedProjection(string semantics)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(2, 1, handlerSemantics: semantics);
        await fixture.UseProductionOwnersAsync();
        var outbox = new DaprProjectionActivationOutbox(fixture.Backend, Options.Create(new ProjectionOptions()), fixture.Clock);
        await outbox.EnsureAsync(fixture.Identity);
        ProjectionActivationWorkItem original = (await outbox.GetAsync(fixture.Identity)).ShouldNotBeNull();
        var replay = Substitute.For<IProjectionUpdateOrchestrator>();
        fixture.StateReadFailure = new InvalidOperationException("transient persisted state unavailable");
        using (var failingWorker = new ProjectionActivationWorker(outbox, replay,
            Options.Create(new ProjectionDispatchOptions()), fixture.Clock, fixture.Probe))
            await failingWorker.RunOnceAsync(CancellationToken.None);
        ProjectionActivationWorkItem deferred = (await outbox.GetAsync(fixture.Identity)).ShouldNotBeNull();
        deferred.Revision.ShouldBe(original.Revision);
        deferred.Attempt.ShouldBe(1);
        fixture.Clock.Advance(deferred.NextDueUtc - fixture.Clock.GetUtcNow());
        fixture.StateReadFailure = null;
        fixture.ReconstructGuardedDelivery();
        var recovered = new DaprProjectionActivationOutbox(fixture.Backend, Options.Create(new ProjectionOptions()), fixture.Clock);
        using var restartedWorker = new ProjectionActivationWorker(recovered, replay,
            Options.Create(new ProjectionDispatchOptions()), fixture.Clock, fixture.Probe);

        await restartedWorker.RunOnceAsync(CancellationToken.None);

        (await recovered.GetAsync(fixture.Identity)).ShouldBeNull();
        var states = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()),
            Options.Create(new ProjectionDeliveryIdempotencyOptions()));
        ProjectionDeliveryState persisted = (await states.ReadAsync(fixture.Identity, fixture.Catalog.OrdinaryProjectionTypes.Single())).State.ShouldNotBeNull();
        persisted.LastDeliveredSequence.ShouldBe(2);
        persisted.ActiveReservation.ShouldBeNull();
        persisted.IdleBindingPending.ShouldBeNull();
    }
}
