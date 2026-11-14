using System.Net;
using System.Text;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Server.Tests.Routing;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Testing.Http;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public partial class EventReplayProjectionActorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReconciliationHostStopCancelsActualLeaseProofWithoutAbandoningItsOwnership(bool stopHost)
    {
        using var caller = new CancellationTokenSource();
        using var hostStopping = new CancellationTokenSource();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(hostStopping.Token);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<HttpResponseMessage>? actualRemoteCall = null;
        CancellationToken actualToken = default;
        bool hold = false;
        static HttpResponseMessage Owned() => new(HttpStatusCode.OK)
        { Content = new StringContent("true", Encoding.UTF8, "application/json") };
        async Task<HttpResponseMessage> HoldProofAsync(CancellationToken token)
        {
            actualToken = token;
            using CancellationTokenRegistration registration = token.Register(() => cancellationObserved.TrySetResult());
            entered.TrySetResult();
            await resume.Task.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return Owned();
        }
        using var handler = new MockHttpMessageHandler((request, token) =>
        {
            request.RequestUri!.AbsolutePath.ShouldEndWith(nameof(IProjectionDeliveryLeaseActor.OwnsDeliveryLeaseAsync));
            return hold ? actualRemoteCall = HoldProofAsync(token) : Task.FromResult(Owned());
        });
        var actorFactory = new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = "http://localhost:3500", UseJsonSerialization = true, JsonSerializerOptions = JsonSerializerOptions.Web,
        }, handler);
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton<IActorProxyFactory>(actorFactory)
            .AddSingleton(lifetime)
            .BuildServiceProvider();
        IProjectionChangeNotifier notifier = Substitute.For<IProjectionChangeNotifier>();
        var actor = ActivatorUtilities.CreateInstance<EventReplayProjectionActor>(services,
            ActorHost.CreateForTest<EventReplayProjectionActor>(new ActorTestOptions { ActorId = new ActorId("counter-projection:tenant-a:counter-1") }),
            Substitute.For<IETagService>(), notifier, NullLogger<EventReplayProjectionActor>.Instance);
        var state = new FaultInjectingActorStateManager();
        ActorStateManagerTestHelper.SetStateManager(actor, state);
        ProjectionReplayPublicationRequest request = PublicationRequest(new(ProjectionLifecyclePhase.Delivering, 1));
        await actor.StageReplayAsync(request).ConfigureAwait(true);
        await actor.PromoteReplayAsync(request).ConfigureAwait(true);
        await actor.FinalizeReplayAsync(request).ConfigureAwait(true);
        string committed = JsonSerializer.Serialize(state.CreateCommittedView());
        int saves = state.Trace.Count(item => item == "SaveState");
        int notifications = notifier.ReceivedCalls().Count();
        var authorities = new RouteCatalogTestFixture();
        await authorities.ActivateAsync(authorities.Candidate()).ConfigureAwait(true);
        RouteCatalogDispatchLease lease = await authorities.Authorities[0].AcquireAsync(default).ConfigureAwait(true);
        var scope = new RouteCatalogLeaseReleaser(lease);
        Task<bool>? pending = null;
        bool disposed = false;
        hold = true;
        try
        {
            pending = actor.ReconcileBoundReplayCompletionAsync(request with { State = null });
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
            Task<bool> callerWait = pending.WaitAsync(caller.Token);
            caller.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(() => callerWait).ConfigureAwait(true);
            actualToken.IsCancellationRequested.ShouldBeFalse();
            cancellationObserved.Task.IsCompleted.ShouldBeFalse();
            if (stopHost)
            {
                hostStopping.Cancel();
                await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(true);
                actualToken.IsCancellationRequested.ShouldBeTrue();
            }
            // Cancellation reaches the real transport; it does not replace its actual completion.
            actualRemoteCall.ShouldNotBeNull().IsCompleted.ShouldBeFalse();
            pending.IsCompleted.ShouldBeFalse();
            (await authorities.Store.ReadAsync(default).ConfigureAwait(true)).State.Leases.ShouldHaveSingleItem();
            JsonSerializer.Serialize(state.CreateCommittedView()).ShouldBe(committed);
            state.Trace.Count(item => item == "SaveState").ShouldBe(saves);
            notifier.ReceivedCalls().Count().ShouldBe(notifications);
            resume.SetResult();
            if (stopHost)
                await Should.ThrowAsync<OperationCanceledException>(() => pending).ConfigureAwait(true);
            else
                (await pending.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true)).ShouldBeTrue();
            actualRemoteCall.IsCompleted.ShouldBeTrue();
            await scope.DisposeAsync().ConfigureAwait(true);
            disposed = true;
            (await authorities.Store.ReadAsync(default).ConfigureAwait(true)).State.Leases.Count.ShouldBe(stopHost ? 1 : 0);
            JsonSerializer.Serialize(state.CreateCommittedView()).ShouldBe(committed);
            state.Trace.Count(item => item == "SaveState").ShouldBe(saves);
            notifier.ReceivedCalls().Count().ShouldBe(notifications);
        }
        finally
        {
            resume.TrySetResult();
            if (pending is not null)
            {
                try { _ = await pending.ConfigureAwait(true); }
                catch { }
            }
            if (!disposed) await scope.DisposeAsync().ConfigureAwait(true);
        }
    }
}
