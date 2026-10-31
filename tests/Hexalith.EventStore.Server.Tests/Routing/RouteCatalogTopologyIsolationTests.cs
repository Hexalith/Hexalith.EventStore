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

    [Theory]
    [InlineData("process", "valid", null)]
    [InlineData("process/*", "valid", null)]
    [InlineData("process", "missing-fence", "missing-effective-finite-receiver-fence")]
    [InlineData("process", "no-parent", "acl-grant-has-no-finite-authenticated-receiver")]
    [InlineData("process", "anonymous-child", "reachable-receiver-is-not-an-exact-authenticated-operation")]
    [InlineData("process/*", "pattern-child", "reachable-receiver-is-not-an-exact-authenticated-operation")]
    [InlineData("process", "wrong-operation", "reachable-receiver-is-not-an-exact-authenticated-operation")]
    [InlineData("process", "wrong-verb", "unbounded-acl-http-verb")]
    [InlineData("process/*", "no-child-inventory", "undeclared-acl-operation")]
    public void PrefixGrantsRequireAnExactFiniteAuthenticatedReceiver(string method, string fault, string? refusal)
    {
        var fixture = new RouteCatalogTestFixture();
        var envelope = SchemaThree(fixture.Envelope);
        const string operation = "domain-service:process";
        var endpoints = new List<RouteCatalogReceiverEndpoint>
        {
            new("/process", ["POST"], operation, ["workload-policy"], false, false, true),
            new("/process/child", ["POST"], operation, ["workload-policy"], false, false, true),
            // Reachable denied endpoints do not gain authority from a prefix transport rule.
            new("/process/{**rest}", ["POST"], null, [], true, true, false),
        };
        if (fault == "no-parent") endpoints.RemoveAt(0);
        if (fault == "anonymous-child") endpoints[1] = endpoints[1] with { Anonymous = true };
        if (fault == "pattern-child") endpoints[1] = endpoints[1] with { Pattern = "/process/{id}", IsPattern = true };
        if (fault == "wrong-operation") endpoints[1] = endpoints[1] with { Operation = "wrong" };
        var receiver = new RouteCatalogReceiverSurface(fault == "missing-fence" ? "missing" : FiniteRouteCatalogReceiver.Version, endpoints);
        string verb = fault == "wrong-verb" ? "GET" : "POST";
        var auxiliary = fault == "no-child-inventory" ? Array.Empty<RouteCatalogAuxiliaryOperation>()
            : new[] { new RouteCatalogAuxiliaryOperation("sample", "process/child", operation, "counter") };
        void Validate() => RouteCatalogTopologyValidator.ValidateDevelopment(fixture.Observations[0].TopologyDocuments[0], Acl(fixture.Hosts[0].AppId, method, verb),
            envelope, fixture.Hosts, "sample", auxiliary, receiver);
        if (refusal is null) Validate();
        else Should.Throw<RouteCatalogValidationException>(Validate).Diagnostics.Single().Code.ShouldBe(refusal);
    }

    [Fact]
    public void UnqualifiedInventoriesAndReadinessGrantsHaveSpecificRefusals()
    {
        var fixture = new RouteCatalogTestFixture();
        var envelope = SchemaThree(fixture.Envelope);
        string[] selected = fixture.Hosts.Select(host => host.AppId).ToArray();
        var component = fixture.Observations[0].TopologyDocuments[0];
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogTopologyValidator.ValidateDevelopment(component, Acl("worker", "ready", "GET"), envelope,
            fixture.Hosts, "sample", null, null, [.. selected, selected[0]], []))
            .Diagnostics.Single().Code.ShouldBe("unqualified-selected-application-inventory");
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogTopologyValidator.ValidateDevelopment(component, Acl("worker", "ready", "GET"), envelope,
            fixture.Hosts, "sample", null, new(FiniteRouteCatalogReceiver.Version, [new("/ready", ["GET"], null, [], false, false, false)]),
            [.. selected, "worker"], [new("worker", "ready", ["GET"])]))
            .Diagnostics.Single().Code.ShouldBe("unqualified-readiness-probe-grant");
    }

    [Theory]
    [InlineData("globstar", "valid")]
    [InlineData("double-scalar", "valid")]
    [InlineData("globstar", "owner")]
    [InlineData("double-scalar", "policy")]
    [InlineData("globstar", "verb")]
    [InlineData("double-scalar", "broader-grant")]
    public void RetainedSdkGrantRequiresExactEndpointOwnerPolicyAndDeclaration(string shape, string fault)
    {
        var fixture = new RouteCatalogTestFixture();
        var envelope = SchemaThree(fixture.Envelope);
        string grant = shape == "globstar" ? "/internal/dead-letters/**" : "/api/v1/commands/status/workload/*/*";
        string pattern = shape == "globstar" ? "/internal/dead-letters/count" : "/api/v1/commands/status/workload/{tenant}/{messageId}";
        string owner = shape == "globstar" ? "Hexalith.EventStore.Operations.Endpoints.DeadLetterOperationsEndpointExtensions"
            : "Hexalith.EventStore.Controllers.CommandStatusController";
        string[] policies = shape == "globstar" ? ["EventStoreHuman", "OperationsHumanRelay:count"] : ["DaprInternal:eventstore:command-status"];
        var http = new RouteCatalogHttpTransport(owner, "GET", RouteCatalogTestFixture.Hash("actual-code")) { Grants = [new(grant, "GET")] };
        const string operation = "sdk-http:retained";
        var endpoint = new RouteCatalogReceiverEndpoint(pattern, ["GET"], operation,
            fault == "policy" ? ["EventStoreHuman"] : policies, false, pattern.Contains('{'), true) { Http = http };
        var auxiliary = new RouteCatalogAuxiliaryOperation(fixture.Hosts[0].AppId, pattern.Trim('/'), operation, "sdk-owner")
        { Http = fault == "owner" ? http with { CodeDigest = RouteCatalogTestFixture.Hash("foreign-code") } : http };
        void Validate() => RouteCatalogTopologyValidator.ValidateDevelopment(fixture.Observations[0].TopologyDocuments[0],
            Acl(fixture.Hosts[0].AppId, fault == "broader-grant" ? "/**" : grant, fault == "verb" ? "POST" : "GET"),
            envelope, fixture.Hosts, fixture.Hosts[0].AppId, [auxiliary], new(FiniteRouteCatalogReceiver.TechnicalVersion, [endpoint]));
        if (fault == "valid") Validate();
        else Should.Throw<RouteCatalogValidationException>(Validate);
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
