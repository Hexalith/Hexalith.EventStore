using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogRebuildScopeTests
{
    [Theory]
    [InlineData("matching-output")]
    [InlineData("other-tenant")]
    [InlineData("other-target")]
    public async Task RequestedNonCallableOutputMustBelongToAdmittedTenantAndExecutableTargetBeforeCheckpointAccess(string scope)
    {
        var fixture = new RouteCatalogTestFixture();
        var original = fixture.Envelope.Declarations.Single(d => d.RouteId == "counter-projection");
        var declaration = original with { Execution = original.Execution! with { ProjectionOutputs = [.. original.Execution!.ProjectionOutputs, "non-callable-output"] } };
        var originalTarget = fixture.Envelope.Routes.Single(r => r.RouteId == original.RouteId);
        var target = originalTarget with { DeclarationDigest = RouteCatalogCodec.ComputeDeclarationDigest(declaration) };
        var declarations = fixture.Envelope.Declarations.Select(d => d.RouteId == original.RouteId && scope == "matching-output" ? declaration : d).ToList();
        var targets = fixture.Envelope.Routes.Select(r => r.RouteId == target.RouteId && scope == "matching-output" ? target : r).ToList();
        if (scope != "matching-output")
        {
            declaration = declaration with { RouteId = "counter-extra-projection", ProjectionType = "extra-projection", Execution = declaration.Execution! with { Aliases = ["extra-projection"], ProjectionOutputs = ["extra-projection", "non-callable-output"] } };
            declarations.Add(declaration);
            targets.Add(target with { RouteId = declaration.RouteId, DeclarationDigest = RouteCatalogCodec.ComputeDeclarationDigest(declaration),
                AppId = scope == "other-target" ? "other-app" : target.AppId, ResolverTenantPattern = scope == "other-tenant" ? "tenant-b" : "tenant-a" });
        }
        var envelope = RouteCatalogCodec.Seal(fixture.Envelope with { Declarations = declarations, Routes = targets });
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 1);
        var authority = Substitute.For<IRouteCatalogAuthority>(); int leases = 0;
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ => { leases++; return new RouteCatalogDispatchLease(snapshot, () => { leases--; return Task.CompletedTask; }); });
        var checkpoints = Substitute.For<IProjectionRebuildCheckpointStore>();
        var resolver = Substitute.For<IDomainServiceResolver>(); var actors = Substitute.For<IActorProxyFactory>();
        var orchestrator = new ProjectionUpdateOrchestrator(authority, actors, Substitute.For<DaprClient>(), Substitute.For<IHttpClientFactory>(), resolver, Substitute.For<IProjectionCheckpointTracker>(), Options.Create(new ProjectionOptions()), NullLogger<ProjectionUpdateOrchestrator>.Instance, rebuildCheckpointStore: checkpoints);
        var request = new ProjectionRebuildCheckpointScope("tenant-a", "counter", "non-callable-output", "aggregate", "operation");
        if (scope == "matching-output")
        {
            await orchestrator.RebuildProjectionAsync(request);
            await checkpoints.Received(1).ReadAsync(request, Arg.Any<CancellationToken>());
        }
        else
        {
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => orchestrator.RebuildProjectionAsync(request));
            checkpoints.ReceivedCalls().ShouldBeEmpty();
        }
        actors.ReceivedCalls().ShouldBeEmpty(); resolver.ReceivedCalls().ShouldBeEmpty(); leases.ShouldBe(0);
    }
}
