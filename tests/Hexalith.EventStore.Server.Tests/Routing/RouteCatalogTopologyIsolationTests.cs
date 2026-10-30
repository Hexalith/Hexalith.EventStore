using System.Text.Json;

using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public class RouteCatalogTopologyIsolationTests
{
    [Fact]
    public void DeclaredAbsentCallersStayInAclButSelectedBusinessCallersRequireEnrollment()
    {
        var fixture = new RouteCatalogTestFixture();
        var envelope = SchemaThree(fixture.Envelope);
        string[] selected = fixture.Hosts.Select(host => host.AppId).ToArray();
        byte[] acl = Acl("declared-worker", "existing/transport", "GET");
        RouteCatalogTopologyValidator.ValidateDevelopment(fixture.Observations[0].TopologyDocuments[0], acl,
            envelope, fixture.Hosts, fixture.Hosts[0].AppId, null, null, selected, [new("declared-worker", "existing/transport", ["GET"])]);
        var active = Should.Throw<RouteCatalogValidationException>(() => RouteCatalogTopologyValidator.ValidateDevelopment(
            fixture.Observations[0].TopologyDocuments[0], acl, envelope, fixture.Hosts, fixture.Hosts[0].AppId,
            null, null, [.. selected, "declared-worker"], [new("declared-worker", "existing/transport", ["GET"])]));
        active.Message.ShouldContain("selected-acl-caller-lacks-catalog-enrollment");
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogTopologyValidator.ValidateDevelopment(
            fixture.Observations[0].TopologyDocuments[0], Acl("undeclared-worker", "existing/transport", "GET"),
            envelope, fixture.Hosts, fixture.Hosts[0].AppId, null, null, selected, [new("declared-worker", "existing/transport", ["GET"])]));
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogTopologyValidator.ValidateDevelopment(
            fixture.Observations[0].TopologyDocuments[0], Acl("declared-worker", "internal/routing/catalog/commit", "POST"),
            envelope, fixture.Hosts, fixture.Hosts[0].AppId, null, null, selected, [new("declared-worker", "existing/transport", ["GET"])]));
    }

    [Fact]
    public void SelectedWorkerCanReadAnExistingExactProbeWithoutAuthorityScope()
    {
        var fixture = new RouteCatalogTestFixture();
        var envelope = SchemaThree(fixture.Envelope);
        var receiver = new RouteCatalogReceiverSurface(FiniteRouteCatalogReceiver.Version,
            [new("/ready", [], null, [], true, false, false)]);
        string[] selected = [.. fixture.Hosts.Select(host => host.AppId), "worker"];
        RouteCatalogTopologyValidator.ValidateDevelopment(fixture.Observations[0].TopologyDocuments[0], Acl("worker", "ready", "GET"),
            envelope, fixture.Hosts, fixture.Hosts[0].AppId, null, receiver, selected, [new("worker", "ready", ["GET"])]);
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogTopologyValidator.ValidateDevelopment(
            fixture.Observations[0].TopologyDocuments[0], Acl("worker", "ready", "POST"), envelope, fixture.Hosts,
            fixture.Hosts[0].AppId, null, receiver, selected, [new("worker", "ready", ["GET"])]));
    }

    [Fact]
    public void SelectedAndOwnerDeclaredCallerInventoriesAreCoveredByObservedIdentity()
    {
        var fixture = new RouteCatalogTestFixture();
        var original = fixture.Observations[0] with
        {
            SelectedAppIds = fixture.Hosts.Select(host => host.AppId).ToArray(), DeclaredInvocations = [new("worker", "existing/transport", ["GET"])],
        };
        var identity = ObservedRouteCatalogHost.Identity(original);
        ObservedRouteCatalogHost.Identity(original with { SelectedAppIds = [.. original.SelectedAppIds!, "worker"] })
            .TopologyDigest.ShouldNotBe(identity.TopologyDigest);
        ObservedRouteCatalogHost.Identity(original with { DeclaredInvocations = [new("other", "existing/transport", ["GET"])] })
            .TopologyDigest.ShouldNotBe(identity.TopologyDigest);
    }

    private static byte[] Acl(string caller, string method, string verb) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        spec = new { accessControl = new { defaultAction = "deny", trustDomain = "public", policies = new[]
        { new { appId = caller, defaultAction = "deny", operations = new[] { new { name = method, action = "allow", httpVerb = new[] { verb } } } } } } },
    });

    private static RouteCatalogEnvelope SchemaThree(RouteCatalogEnvelope original)
    {
        var operations = original.Declarations.Select(declaration => new RouteOperationContract(declaration.RouteId, declaration.ContractVersion,
            declaration.Kind == "projection" || declaration.Execution!.RequiresDelegatedContext ? "internal" : "public", "agent-eligible",
            JsonSerializer.SerializeToElement(new { type = "object" }), JsonSerializer.SerializeToElement(new { type = "object" }), string.Empty,
            declaration.Kind, declaration.Domain, declaration.MessageType ?? declaration.ProjectionType))
            .Select(operation => operation with { Digest = RouteCatalogCodec.ComputeOperationContractDigest(operation) }).ToArray();
        return VersionedRouteCatalogBuilder.Build(new(1, "Development", original.Declarations, operations,
            original.Routes.Select(route => new RouteCompositionBinding(route.RouteId, route.AppId, route.Method, route.ContractVersion, false, route.ResolverTenantPattern)).ToArray(), original.Idempotency));
    }
}
