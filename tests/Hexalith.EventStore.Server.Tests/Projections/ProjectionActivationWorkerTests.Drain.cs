using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Tests.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class ProjectionActivationWorkerTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CanceledProtocolWaitKeepsHostedSlotAndCatalogLeaseUntilActualNativeCallEndsWithoutClearingAmbiguity(bool ambiguous, bool cancelStopWait)
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(1);
        var catalog = new RouteCatalogTestFixture();
        await catalog.ActivateAsync(catalog.Candidate());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var guard = Substitute.For<IProjectionSourceGuardDelivery>();
        Task<ProjectionSourceGuardDeliveryResult>? nativeCall = null;
        guard.TryDeliverAsync(fixture.Identity, Arg.Any<CancellationToken>()).Returns(call =>
        {
            // Existing actor helpers can stop awaiting a native RPC when the host token
            // cancels. Its actual boundary remains enrolled independently of that wait.
            nativeCall = RouteCatalogRemoteCall.InvokeAsync(async () =>
            {
                await resume.Task;
                if (ambiguous) throw new IOException("native completion ambiguous");
                return new ProjectionSourceGuardDeliveryResult(true, true, null) { FinalizedCleanupProven = true };
            });
            entered.TrySetResult();
            return nativeCall.WaitAsync(call.Arg<CancellationToken>());
        });
        using var worker = new ProjectionActivationWorker(fixture.Outbox, Substitute.For<IProjectionUpdateOrchestrator>(),
            Options.Create(new ProjectionDispatchOptions { RetryWorkerInterval = TimeSpan.FromMilliseconds(10) }),
            TimeProvider.System, guard, catalog.Authorities[0]);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var stopDeadline = new CancellationTokenSource();
            Task stopping = worker.StopAsync(stopDeadline.Token);
            stopping.IsCompleted.ShouldBeFalse();
            if (cancelStopWait)
            {
                stopDeadline.Cancel();
                await stopping.WaitAsync(TimeSpan.FromSeconds(5));
                // The caller's wait has ended; the actual owned execution and RPC have not.
                worker.IsRunning.ShouldBeFalse();
                worker.ExecuteTask.ShouldNotBeNull().IsCompleted.ShouldBeFalse();
                nativeCall.ShouldNotBeNull().IsCompleted.ShouldBeFalse();
            }
            (await catalog.Store.ReadAsync(default)).State.Leases.ShouldHaveSingleItem();
            (await fixture.Outbox.GetAsync(fixture.Identity)).ShouldNotBeNull();
            resume.SetResult();
            if (ambiguous) await Should.ThrowAsync<IOException>(() => nativeCall.ShouldNotBeNull());
            else await nativeCall.ShouldNotBeNull();
            await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            worker.ExecuteTask.ShouldNotBeNull().IsCompleted.ShouldBeTrue();
            (await catalog.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(ambiguous ? 1 : 0);
            // A canceled wait never claimed projection completion or removed recoverable work.
            (await fixture.Outbox.GetAsync(fixture.Identity)).ShouldNotBeNull();
        }
        finally { resume.TrySetResult(); await worker.StopAsync(CancellationToken.None); }
    }
}
