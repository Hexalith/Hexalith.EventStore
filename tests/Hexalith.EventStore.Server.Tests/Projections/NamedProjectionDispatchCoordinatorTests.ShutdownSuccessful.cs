using System.Net;
using System.Text;
using System.Text.Json;

using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Tests.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Testing.Http;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class NamedProjectionDispatchCoordinatorTests
{
    /// <summary>Holds successful dispatch final lifecycle completion independently of the admission-denied branch.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HostShutdownCancelsSuccessfulDispatchFinalLifecycleCompletionWithoutDroppingOwnership(bool stopHost)
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
        async Task<HttpResponseMessage> HoldCompletionAsync(CancellationToken token)
        {
            actualToken = token;
            using CancellationTokenRegistration registration = token.Register(() => cancellationObserved.TrySetResult());
            entered.TrySetResult();
            await resume.Task.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("true", Encoding.UTF8, "application/json") };
        }
        using var handler = new MockHttpMessageHandler((request, token) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith(nameof(IProjectionLifecycleActor.BeginDeliveryWriteAsync), StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("true", Encoding.UTF8, "application/json") });
            request.RequestUri.AbsolutePath.ShouldEndWith(nameof(IProjectionLifecycleActor.CompleteDeliveryWriteAsync));
            return actualRemoteCall = HoldCompletionAsync(token);
        });
        var actorFactory = new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = "http://localhost:3500", UseJsonSerialization = true, JsonSerializerOptions = JsonSerializerOptions.Web,
        }, handler);
        var idempotency = Substitute.For<IProjectionDeliveryIdempotencyCoordinator>();
        idempotency.TryAdmitAsync(Identity, "widget-detail", Arg.Any<IReadOnlyList<ProjectionEventDto>>(), true,
            Arg.Any<CancellationToken>(), Arg.Any<long?>()).Returns(new ProjectionDeliveryAdmissionResult(
                ProjectionDeliveryAdmissionDisposition.Dispatch, null, Reservation("message-1")));
        idempotency.CompleteAsync(Identity, "widget-detail", Arg.Any<IReadOnlyList<ProjectionEventDto>>(),
            Arg.Any<ProjectionDeliveryReservation>(), Arg.Any<CancellationToken>()).Returns(ProjectionDeliveryCompletion.Completed);
        var catalog = new NamedProjectionRouteCatalog();
        catalog.Replace(Snapshot("fingerprint", "widget-detail"));
        var scheduler = new MemoryRetryScheduler();
        var wire = new ProjectionDispatchHttpMessageHandler(JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version, [new("widget-detail", ProjectionDispatchStatus.Completed, null, null)])));
        var clients = Substitute.For<IHttpClientFactory>();
        clients.CreateClient(Arg.Any<string>()).Returns(new HttpClient(wire));
        var coordinator = new NamedProjectionDispatchCoordinator(catalog, Substitute.For<IProjectionDeliveryCheckpointStore>(),
            new DaprProjectionLifecycleGateway(actorFactory), actorFactory, new DaprClientBuilder().Build(), clients,
            Options.Create(new ProjectionDispatchOptions()), NullLogger<NamedProjectionDispatchCoordinator>.Instance,
            scheduler, idempotencyCoordinator: idempotency, hostApplicationLifetime: lifetime);
        var authorities = new RouteCatalogTestFixture();
        await authorities.ActivateAsync(authorities.Candidate()).ConfigureAwait(true);
        RouteCatalogDispatchLease lease = await authorities.Authorities[0].AcquireAsync(default).ConfigureAwait(true);
        var scope = new RouteCatalogLeaseReleaser(lease);
        Task<bool>? pending = null;
        bool disposed = false;
        try
        {
            pending = coordinator.TryDispatchHeadAsync(Identity, Registration(), 1, Envelope(1).MessageId, [ProjectionEvent(1)], caller.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
            caller.Cancel();
            if (stopHost)
            {
                hostStopping.Cancel();
                await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(true);
                actualToken.IsCancellationRequested.ShouldBeTrue();
            }
            else
            {
                actualToken.IsCancellationRequested.ShouldBeFalse();
                cancellationObserved.Task.IsCompleted.ShouldBeFalse();
            }
            // Even an observed cancellation does not abandon a still-running actual transport task.
            actualRemoteCall.ShouldNotBeNull().IsCompleted.ShouldBeFalse();
            pending.IsCompleted.ShouldBeFalse();
            (await authorities.Store.ReadAsync(default).ConfigureAwait(true)).State.Leases.ShouldHaveSingleItem();
            ProjectionDeliveryRetryWorkItem original = scheduler.Work.ShouldNotBeNull();
            original.PendingRoutes.ShouldBe(["widget-detail"]);
            wire.Requests.ShouldHaveSingleItem().Path.ShouldEndWith("/project/v2");
            _ = await idempotency.Received(1).CompleteAsync(Identity, "widget-detail",
                Arg.Any<IReadOnlyList<ProjectionEventDto>>(), Arg.Any<ProjectionDeliveryReservation>(),
                Arg.Any<CancellationToken>()).ConfigureAwait(true);
            resume.SetResult();
            Exception? failure = null;
            try { _ = await pending.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true); }
            catch (Exception exception) { failure = exception; }
            actualRemoteCall.IsCompleted.ShouldBeTrue();
            if (stopHost) failure.ShouldNotBeNull();
            else actualRemoteCall.IsCompletedSuccessfully.ShouldBeTrue();
            if (stopHost) scheduler.Work.ShouldNotBeNull().DispatchId.ShouldBe(original.DispatchId);
            await scope.DisposeAsync().ConfigureAwait(true);
            disposed = true;
            (await authorities.Store.ReadAsync(default).ConfigureAwait(true)).State.Leases.Count.ShouldBe(stopHost ? 1 : 0);
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
