using System.Text.Json;

using Dapr.Actors.Client;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Tenants.Server.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Exercises independently observed admission, actual activation and dispatch under default and selected physical gateway identities.</summary>
public sealed class DevelopmentGatewayIdentityTests
{
    /// <summary>A mapped Dapr gateway must agree with the actual installed admission policy before effects.</summary>
    [Theory]
    [InlineData("eventstore")]
    [InlineData("eventstore-owned-session")]
    public async Task PhysicalGatewayIdentityBindsObservedPoliciesAndCommittedDispatch(string physicalAppId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(physicalAppId);
        var fixture = new RouteCatalogTestFixture();
        var services = new ServiceCollection();
        RouteCatalogAdmissionFixture.Configure(services, [.. SampleCatalog.CreateAdapters(), .. TenantIntentAdapters.Create()]);
        using var provider = services.BuildServiceProvider();
        var actualAdmission = await RouteCatalogAdmissionObservation.CaptureAsync(provider, physicalAppId, TestContext.Current.CancellationToken);
        var envelope = RouteCatalogCodec.Seal(fixture.Envelope with
        {
            Routes = fixture.Envelope.Routes.Select(route => route.AppId == "eventstore" ? route with { AppId = physicalAppId } : route).ToArray(),
            Idempotency = actualAdmission,
        });
        string[] appIds = fixture.Observations.Select(observation => observation.AppId == "eventstore" ? physicalAppId : observation.AppId).ToArray();
        var component = JsonSerializer.SerializeToUtf8Bytes(new
        {
            kind = "Component", metadata = new { name = fixture.Store.Coordinates.StateStore },
            spec = new { type = "state.redis", metadata = new[] { new { name = "keyPrefix", value = "none" } } }, scopes = appIds,
        });
        var observations = fixture.Observations.Select(observation => observation with
        {
            AppId = observation.AppId == "eventstore" ? physicalAppId : observation.AppId,
            Targets = observation.Targets.Select(target => target.AppId == "eventstore" ? target with { AppId = physicalAppId } : target).ToArray(),
            AdmissionPolicies = observation.AppId == "eventstore" ? actualAdmission : [],
            TopologyDocuments = [component, observation.TopologyDocuments[1]],
        }).ToArray();
        var hosts = observations.Select(ObservedRouteCatalogHost.Identity).ToArray();
        var environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns("Development");
        var authorities = hosts.Select(host =>
        {
            var options = Options.Create(new RouteCatalogHostOptions { Identity = host, Profile = "Development", DevelopmentFixture = true });
            return new RouteCatalogAuthority(fixture.Store, new DevelopmentRouteCatalogTrustValidator(options, environment), options,
                new ObservedRouteCatalogHost(_ => Task.FromResult(observations.Single(observation => observation.HostId == host.HostId))));
        }).ToArray();
        var candidate = new RouteCatalogActivation("physical-gateway", RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), null,
            hosts, "Development-fixture", string.Empty);
        candidate = candidate with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(candidate) };
        await authorities[0].PrepareAsync(candidate, TestContext.Current.CancellationToken);
        foreach (var authority in authorities) await authority.ReadyAsync(candidate.AttemptId, TestContext.Current.CancellationToken);
        await authorities[0].CommitAsync(candidate.AttemptId, TestContext.Current.CancellationToken);
        (await authorities[0].ReadbackAsync(TestContext.Current.CancellationToken)).RequiredHosts.ShouldBe(hosts);
        var command = new SubmitCommand("01M61800000000000000000001", "tenant-a", "counter", "counter-1", "increment-counter",
            "{\"counterId\":\"counter-1\"}"u8.ToArray(), "01M61800000000000000000002", "fixture-user");
        var actors = Substitute.For<IActorProxyFactory>(); var keys = Substitute.For<IIdempotencyDigestKeyProvider>();
        var admissionOptions = Options.Create(new IdempotencyAdmissionOptions { Enabled = true, ActiveDigestKeyVersion = "development-v1" });
        IdempotencyAdmissionCoordinator Coordinator(string identity) => new(authorities[0], actors, new IdempotencyKeyProtector(keys),
            Substitute.For<IIdempotencyIntentAdapterRegistry>(), admissionOptions: admissionOptions,
            catalogOptions: Options.Create(new RouteCatalogHostOptions { Identity = hosts[0] with { AppId = identity } }));
        (await Coordinator(physicalAppId).AdmitAsync(command, TestContext.Current.CancellationToken)).ShouldBeNull();
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => Coordinator("wrong-consumer").AdmitAsync(command, TestContext.Current.CancellationToken));
        actors.ReceivedCalls().ShouldBeEmpty(); keys.ReceivedCalls().ShouldBeEmpty();
        var router = Substitute.For<ICommandRouter>();
        router.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>()).Returns(new CommandProcessingResult(true));
        var handler = new SubmitCommandHandler(authorities[0], Substitute.For<ICommandStatusStore>(), Substitute.For<ICommandArchiveStore>(), router,
            null, null, new NoOpProjectionUpdateOrchestrator(), NullLogger<SubmitCommandHandler>.Instance);
        (await handler.Handle(command, TestContext.Current.CancellationToken)).MessageId.ShouldBe(command.MessageId);
        await router.Received(1).RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
        (await fixture.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldBeEmpty();
    }
}
