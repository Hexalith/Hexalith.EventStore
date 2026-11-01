using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;
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
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CanceledCommitWithOldHeadRemovedRestoresIndependentlyAndReconciliationRepairsFailedRestoration(bool failRestoration, bool removeOwnership)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(1, 1);
        await fixture.UseProductionOwnersAsync();
        var persisted = new DaprProjectionActivationOutbox(fixture.Backend, Options.Create(new ProjectionOptions()), fixture.Clock);
        var forwarding = Substitute.For<IProjectionActivationOutbox>();
        using var caller = new CancellationTokenSource();
        int writes = 0;
        forwarding.EnsureAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            CancellationToken token = call.Arg<CancellationToken>();
            if (++writes > 1)
            {
                token.ShouldNotBe(caller.Token);
                token.IsCancellationRequested.ShouldBeFalse();
                if (writes == 2 && failRestoration) throw new IOException("durable restore unavailable");
            }
            await persisted.EnsureAsync(call.Arg<AggregateIdentity>(), token);
        });
        SubmitCommand command = Command() with { IdempotencyKey = "protected-key" };
        var execution = new IdempotencyExecutionContext(IdempotencyExecutionContext.CurrentSchemaVersion,
            "tenant-a:v1:digest", 3, "v1", command.MessageId, command.CorrelationId,
            command.Tenant, command.Domain, command.AggregateId, command.CommandType, "proof");
        var session = new IdempotencyAdmissionSession("tenant-a:v1:digest", 3, IdempotencyAdmissionDecision.Execute,
            null, execution, command.MessageId, command.CorrelationId);
        var admission = Substitute.For<IIdempotencyAdmissionCoordinator>();
        admission.AdmitAsync(command, Arg.Any<CancellationToken>()).Returns(_ => session);
        var result = new CommandProcessingResult(true, EventCount: 1, ResultPayload: "{\"count\":2}");
        var replay = Substitute.For<IProjectionUpdateOrchestrator>();
        var fallbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumeFallback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        replay.UpdateProjectionAsync(Arg.Any<AggregateIdentity>(), CancellationToken.None).Returns(_ =>
        {
            fallbackEntered.TrySetResult();
            return resumeFallback.Task;
        });
        using var oldWorker = new ProjectionActivationWorker(persisted, replay,
            Options.Create(new ProjectionDispatchOptions()), fixture.Clock, fixture.Probe);
        var router = Substitute.For<ICommandRouter>();
        ProjectionActivationWorkItem? sampled = null;
        router.RouteFencedCommandAsync(Arg.Any<SubmitCommand>(), execution, caller.Token).Returns(async _ =>
        {
            sampled = (await persisted.GetAsync(fixture.Identity)).ShouldNotBeNull();
            await oldWorker.RunOnceAsync(CancellationToken.None);
            (await persisted.GetAsync(fixture.Identity)).ShouldBeNull();
            await fixture.AppendPersistedSourceEventAsync();
            caller.Cancel();
            return result;
        });
        router.ReconcileFencedCommandAsync(Arg.Any<SubmitCommand>(), execution, Arg.Any<CancellationToken>())
            .Returns(new IdempotencyCheckResult(IdempotencyCheckOutcome.ExactTerminalDuplicate, result));
        using ServiceProvider provider = BuildProvider(fixture.Backend, forwarding, router, replay,
            clock: fixture.Clock, coordinator: admission);
        SubmitCommandHandler handler = provider.GetRequiredService<SubmitCommandHandler>();

        if (failRestoration) await Should.ThrowAsync<IOException>(() => handler.Handle(command, caller.Token));
        else await Should.ThrowAsync<OperationCanceledException>(() => handler.Handle(command, caller.Token));
        await admission.Received(1).MarkRecoveryAsync(Arg.Any<IdempotencyAdmissionSession>(),
            IdempotencyAdmissionState.UnknownProviderOutcome, CancellationToken.None);
        await admission.DidNotReceiveWithAnyArgs().CompleteAsync(default!, default!, default);
        (await persisted.GetAsync(fixture.Identity) is not null).ShouldBe(!failRestoration);

        session = session with { Decision = IdempotencyAdmissionDecision.UnknownProviderOutcome };
        admission.CompleteAsync(Arg.Any<IdempotencyAdmissionSession>(), result, Arg.Any<CancellationToken>())
            .Returns(async _ => { (await persisted.GetAsync(fixture.Identity)).ShouldNotBeNull(); });
        using ServiceProvider reconciliationScope = BuildProvider(fixture.Backend, forwarding, router, replay,
            workersEnabled: !removeOwnership, clock: fixture.Clock, coordinator: admission);
        Task<SubmitCommandResult> response = reconciliationScope.GetRequiredService<SubmitCommandHandler>()
            .Handle(command, CancellationToken.None);
        SubmitCommandResult reconciled;
        try
        {
            if (removeOwnership)
            {
                await fallbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                response.IsCompleted.ShouldBeFalse();
                (await persisted.GetAsync(fixture.Identity)).ShouldNotBeNull();
                await admission.DidNotReceiveWithAnyArgs().CompleteAsync(default!, default!, default);
                resumeFallback.SetResult();
            }
            reconciled = await response.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { resumeFallback.TrySetResult(); }
        reconciled.ResultPayload.ShouldBe(result.ResultPayload);
        writes.ShouldBe(3);
        ProjectionActivationWorkItem restored = (await persisted.GetAsync(fixture.Identity)).ShouldNotBeNull();
        restored.Revision.ShouldBeGreaterThan(sampled.ShouldNotBeNull().Revision);
        await persisted.CompleteAsync(sampled!);
        (await persisted.GetAsync(fixture.Identity)).ShouldBe(restored);
        fixture.ReconstructGuardedDelivery();
        using var recovery = new ProjectionActivationWorker(persisted, replay,
            Options.Create(new ProjectionDispatchOptions()), fixture.Clock, fixture.Probe);
        await recovery.RunOnceAsync(CancellationToken.None);
        (await persisted.GetAsync(fixture.Identity)).ShouldBeNull();
        var model = await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + fixture.Catalog.OrdinaryProjectionTypes.Single());
        model.Value.ShouldNotBeNull().Count.ShouldBe(2);
        await router.Received(1).RouteFencedCommandAsync(Arg.Any<SubmitCommand>(), execution, caller.Token);
        await replay.Received(removeOwnership ? 1 : 0).UpdateProjectionAsync(Arg.Any<AggregateIdentity>(), CancellationToken.None);
    }
}
