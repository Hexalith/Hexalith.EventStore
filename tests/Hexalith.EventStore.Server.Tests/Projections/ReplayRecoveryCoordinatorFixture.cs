using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>Production named delivery, row CAS, handler and batch persistence across fresh request scopes.</summary>
internal sealed class ReplayRecoveryCoordinatorFixture : IDisposable
{
    internal ReplayRecoveryProductionFixture Host { get; }
    internal ReplayRecoveryProductionHttpHandler Http { get; }
    internal DaprProjectionDeliveryStateStore States { get; }
    internal ProjectionDeliveryRetryWorkItem? Work { get; private set; }
    internal NamedProjectionRouteCatalogEntry Catalog { get; set; }
    internal AggregateIdentity Identity { get; } = new("tenant-a", "counter", "counter-1");
    private readonly NamedProjectionDispatchCoordinator _coordinator;
    private readonly HttpClient _http;
    private readonly DaprClient _invocation = new DaprClientBuilder().Build();

    internal ReplayRecoveryCoordinatorFixture(int head = 3, int routeCount = 1)
    {
        Host = new(head, routeCount);
        Http = new(Host);
        _http = new(Http);
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-10-16T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        Catalog = new("sample", "v1", "counter", 2, ProjectionDispatchProtocol.Capability,
            Host.Request.CatalogFingerprint, Host.Request.ProjectionTypes, Host.Request.ProjectionTypes)
            { ReplayRecoveryBindings = Host.Metadata.Domains.Single().ReplayRecoveryBindings! };
        INamedProjectionRouteCatalog catalogs = Substitute.For<INamedProjectionRouteCatalog>();
        catalogs.Current.Returns(_ => new NamedProjectionRouteCatalogSnapshot([Catalog]));
        var projectionOptions = Options.Create(new ProjectionOptions());
        States = new(Host.Backend, projectionOptions);
        IProjectionLifecycleGateway lifecycle = Substitute.For<IProjectionLifecycleGateway>();
        lifecycle.BeginDeliveryWriteAsync(Identity, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        lifecycle.CompleteDeliveryWriteAsync(Identity, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        IProjectionDeliveryRetryScheduler scheduler = Substitute.For<IProjectionDeliveryRetryScheduler>();
        scheduler.ScheduleAsync(Arg.Any<ProjectionDeliveryRetryWorkItem>(), Arg.Any<CancellationToken>()).Returns(call => Work ??= call.ArgAt<ProjectionDeliveryRetryWorkItem>(0));
        scheduler.TryAcquireAsync(Arg.Any<ProjectionDeliveryRetryWorkItem>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (Work is null || Work.Revision != call.ArgAt<ProjectionDeliveryRetryWorkItem>(0).Revision) return null;
            Work = Work with { Revision = Work.Revision + 1, LeaseOwner = call.ArgAt<string>(1), LeaseExpiresUtc = clock.GetUtcNow().AddMinutes(5) };
            return Work;
        });
        scheduler.TryUpdateAsync(Arg.Any<ProjectionDeliveryRetryWorkItem>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            ProjectionDeliveryRetryWorkItem proposed = call.ArgAt<ProjectionDeliveryRetryWorkItem>(0);
            if (Work is null || proposed.Revision != Work.Revision || proposed.LeaseOwner != Work.LeaseOwner) return false;
            Work = proposed with { Revision = proposed.Revision + 1, LeaseOwner = null, LeaseExpiresUtc = null };
            return true;
        });
        scheduler.TryDeleteAsync(Arg.Any<ProjectionDeliveryRetryWorkItem>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (Work is null || Work.Revision != call.ArgAt<ProjectionDeliveryRetryWorkItem>(0).Revision) return false;
            Work = null;
            return true;
        });
        IHttpClientFactory clients = Substitute.For<IHttpClientFactory>();
        clients.CreateClient(Arg.Any<string>()).Returns(_http);
        _coordinator = new(catalogs, Substitute.For<IProjectionDeliveryCheckpointStore>(), lifecycle,
            Substitute.For<IActorProxyFactory>(), _invocation, clients, Options.Create(new ProjectionDispatchOptions()),
            NullLogger<NamedProjectionDispatchCoordinator>.Instance, scheduler, clock,
            idempotencyCoordinator: new ProjectionDeliveryIdempotencyCoordinator(States,
                Options.Create(new ProjectionDeliveryIdempotencyOptions()), clock));
    }

    internal Task<ProjectionDeliveryCutoverStatus> ActivateFixtureWriterAsync() => new ProjectionDeliveryCutover(States,
        TimeProvider.System).ActivateAsync(new("fixture-baseline", "fixture-backup", true, true, true));

    internal Task<bool> DispatchAsync() => _coordinator.TryDispatchCompatibleAsync(Identity, Catalog, Host.Request.Request.Events.Length,
        Host.Request.DispatchId, Host.Request.Request.Events, Host.Request.ProjectionTypes, CancellationToken.None);

    public void Dispose()
    {
        _http.Dispose();
        _invocation.Dispose();
        Host.Dispose();
    }
}
