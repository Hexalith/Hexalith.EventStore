using System.Text.Json;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogQueryRuntimeCoreTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequiredAuthorityRuntimeCoreAndDefaultRegistrationRetainSourceProvenanceCollaborator(bool registration)
    {
        var fixture = new RouteCatalogTestFixture(); await fixture.ActivateAsync(fixture.Candidate());
        var declaration = fixture.Envelope.Declarations.First(d => d.Kind == "query" && d.Execution!.TargetKind == "projection-actor");
        var invoker = Substitute.For<IProjectionActorInvoker>();
        invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { count = 9 }), declaration.ProjectionType));
        var lifecycle = Substitute.For<IProjectionLifecycleGateway>();
        lifecycle.ReadSnapshotAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new ProjectionLifecycleSnapshot(ProjectionLifecyclePhase.Idle, 1));
        var source = Substitute.For<IProjectionSourceGuardDelivery>();
        source.InspectCurrentAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new ProjectionSourceGuardDeliveryResult(false, false, null));
        Should.Throw<ArgumentNullException>(() => new QueryRouter(null!, invoker, NullLogger<QueryRouter>.Instance, lifecycle, source));
        var services = new ServiceCollection(); services.AddLogging();
        services.AddSingleton<IRouteCatalogAuthority>(fixture.Authorities[0]); services.AddSingleton(invoker); services.AddSingleton(lifecycle); services.AddSingleton(source);
        services.AddEventStoreServer(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        var router = registration ? provider.GetRequiredService<QueryRouter>()
            : new QueryRouter(fixture.Authorities[0], invoker, NullLogger<QueryRouter>.Instance, lifecycle, source);
        var result = await router.RouteQueryAsync(new SubmitQuery("tenant-a", declaration.Domain, "aggregate", declaration.MessageType!, [], "correlation", "user"));
        result.Success.ShouldBeTrue(); result.Payload!.Value.GetProperty("count").GetInt32().ShouldBe(9);
        await source.Received(1).InspectCurrentAsync(Arg.Any<AggregateIdentity>(), declaration.ProjectionType!, Arg.Any<CancellationToken>());
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }
}
