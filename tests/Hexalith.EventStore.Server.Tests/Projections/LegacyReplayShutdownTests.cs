using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Tests.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Hosting;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>Verifies the ownership and lifetime of compatible replay finalization.</summary>
public sealed class LegacyReplayShutdownTests
{
    /// <summary>Preserves caller-independent cleanup and its original deadline while host shutdown cancels the actual owned call.</summary>
    [Theory]
    [InlineData("caller")]
    [InlineData("host")]
    [InlineData("deadline")]
    public async Task FinalizationRetainsActualCallAndUnknownFenceUntilCompletion(string cancellation)
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(1).ConfigureAwait(true);
        using var caller = new CancellationTokenSource();
        using var hostStopping = new CancellationTokenSource();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(hostStopping.Token);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken actualToken = default;
        Task<bool>? actualCall = null;
        async Task<bool> HoldCompletionAsync(string lease, CancellationToken token)
        {
            actualToken = token;
            using CancellationTokenRegistration registration = token.Register(() => cancellationObserved.TrySetResult());
            entered.TrySetResult();
            await resume.Task.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return await fixture.LifecycleActor.CompleteDeliveryWriteAsync(new(lease)).ConfigureAwait(false);
        }
        fixture.Lifecycle.CompleteDeliveryWriteAsync(fixture.Identity, "counter", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => actualCall = HoldCompletionAsync(call.ArgAt<string>(2), call.ArgAt<CancellationToken>(3)));
        var catalog = new RouteCatalogTestFixture();
        await catalog.ActivateAsync(catalog.Candidate()).ConfigureAwait(true);
        RouteCatalogDispatchLease lease = await catalog.Authorities[0].AcquireAsync(default).ConfigureAwait(true);
        var scope = new RouteCatalogLeaseReleaser(lease);
        Task<bool>? pending = null;
        bool disposed = false;
        try
        {
            pending = fixture.CreateOrchestrator(hostApplicationLifetime: lifetime).DeliverCompatibleReplayAsync(fixture.Identity,
                new("sample", "project", fixture.Identity.TenantId, fixture.Identity.Domain, "v1"), null,
                fixture, fixture, fixture.Outbox, caller.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
            ProjectionActivationWorkItem original = (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldNotBeNull();
            original.LegacyDelivery.ShouldNotBeNull();
            caller.Cancel();
            actualToken.IsCancellationRequested.ShouldBeFalse();
            if (cancellation == "host")
            {
                hostStopping.Cancel();
                await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(true);
            }
            else if (cancellation == "deadline")
            {
                fixture.Clock.Advance(TimeSpan.FromSeconds(29));
                actualToken.IsCancellationRequested.ShouldBeFalse();
                fixture.Clock.Advance(TimeSpan.FromSeconds(1));
                await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(true);
            }
            actualToken.IsCancellationRequested.ShouldBe(cancellation != "caller");
            actualCall.ShouldNotBeNull().IsCompleted.ShouldBeFalse();
            pending.IsCompleted.ShouldBeFalse();
            (await catalog.Store.ReadAsync(default).ConfigureAwait(true)).State.Leases.ShouldHaveSingleItem();
            resume.SetResult();
            try
            {
                _ = await pending.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (cancellation != "caller")
            {
            }
            actualCall.IsCompleted.ShouldBeTrue();
            await scope.DisposeAsync().ConfigureAwait(true);
            disposed = true;
            (await catalog.Store.ReadAsync(default).ConfigureAwait(true)).State.Leases.Count.ShouldBe(cancellation == "caller" ? 0 : 1);
            if (cancellation == "caller")
            {
                (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldBeNull();
                (await fixture.LifecycleActor.ReadSnapshotAsync().ConfigureAwait(true)).Phase.ShouldBe(ProjectionLifecyclePhase.Idle);
            }
            else
            {
                (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldNotBeNull().LegacyDelivery
                    .ShouldNotBeNull().OperationId.ShouldBe(original.LegacyDelivery.OperationId);
            }
        }
        finally
        {
            resume.TrySetResult();
            if (pending is not null)
            {
                try
                {
                    _ = await pending.ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                }
            }
            if (!disposed)
            {
                await scope.DisposeAsync().ConfigureAwait(true);
            }
        }
    }
}
