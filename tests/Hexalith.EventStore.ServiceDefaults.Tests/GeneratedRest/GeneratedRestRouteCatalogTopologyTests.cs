using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Shouldly;

namespace Hexalith.EventStore.ServiceDefaults.Tests.GeneratedRest;

/// <summary>Exercises the complete topology boundary with selected proxies outside the authority cohort.</summary>
public sealed class GeneratedRestRouteCatalogTopologyTests
{
    /// <summary>Dedicated shared authorities isolate business keys while legacy none remains explicitly compatible.</summary>
    [Theory]
    [InlineData("name", true)]
    [InlineData("none", true)]
    [InlineData("appid", false)]
    [InlineData("namespace", false)]
    [InlineData("missing-role", false)]
    [InlineData("actor-true", false)]
    [InlineData("actor-one", false)]
    [InlineData("actor-false", true)]
    [InlineData("scope", false)]
    [InlineData("empty-name", false)]
    [InlineData("malformed-name", false)]
    [InlineData("duplicate-prefix", false)]
    public void CompleteTopologyQualifiesSharedAuthorityKeyspaceAndRole(string scenario, bool admitted)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        string digest = new('a', 64);
        RouteCatalogHostIdentity[] hosts = [new("domain", "domain", digest, digest, digest)];
        var component = new JsonObject
        {
            ["kind"] = "Component",
            ["metadata"] = new JsonObject { ["name"] = scenario == "empty-name" ? "" : scenario == "malformed-name" ? "invalid/name" : "routing-owned",
                ["labels"] = new JsonObject { [RouteCatalogAuthorityComponentPolicy.RoleLabel] = scenario == "missing-role" ? "unknown" : RouteCatalogAuthorityComponentPolicy.IsolatedRole } },
            ["spec"] = new JsonObject { ["type"] = "state.redis", ["metadata"] = new JsonArray(new JsonObject
                { ["name"] = "keyPrefix", ["value"] = scenario is "none" or "appid" or "namespace" ? scenario : "name" }) },
            ["scopes"] = new JsonArray(scenario == "scope" ? "unrelated" : "domain"),
        };
        if (scenario.StartsWith("actor-", StringComparison.Ordinal)) component["spec"]!["metadata"]!.AsArray().Add(new JsonObject
            { ["name"] = "actorStateStore", ["value"] = scenario == "actor-true" ? "true" : scenario == "actor-one" ? "1" : "false" });
        if (scenario == "duplicate-prefix") component["spec"]!["metadata"]!.AsArray().Add(new JsonObject { ["name"] = "keyPrefix", ["value"] = "name" });
        byte[] acl = JsonSerializer.SerializeToUtf8Bytes(new { spec = new { accessControl = new { defaultAction = "deny", trustDomain = "public" } } });
        Action validate = () => RouteCatalogTopologyValidator.ValidateDevelopment(JsonSerializer.SerializeToUtf8Bytes(component), acl,
            GeneratedRestProbeDeclarations.Catalog(), hosts, "domain");
        if (admitted) Should.NotThrow(validate); else Should.Throw<RouteCatalogValidationException>(validate);
    }

    /// <summary>Only independently declared, actually typed human GET ingress exempts a selected proxy from enrollment.</summary>
    [Theory]
    [InlineData("typed-get", true)]
    [InlineData("post", false)]
    [InlineData("service", false)]
    [InlineData("control", false)]
    [InlineData("missing-typed", false)]
    [InlineData("tampered-typed", false)]
    [InlineData("undeclared", false)]
    [InlineData("enrolled-control", true)]
    public void CompleteTopologyRetainsSelectedCallerEnrollmentBoundary(string scenario, bool admitted)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        RouteCatalogEnvelope catalog = GeneratedRestProbeDeclarations.Catalog();
        var declaration = catalog.Declarations.Single();
        var typed = new RouteCatalogGeneratedRestEndpoint("/api/probe/{tenantId}", declaration.RouteId,
            RouteCatalogCodec.ComputeDeclarationDigest(declaration), catalog.Operations!.Single().Digest,
            typeof(GeneratedRestProbeQuery).FullName!, [EventStoreHttpHostSecurityExtensions.HumanPolicy]);
        string control = RouteCatalogEndpointExtensions.ReadbackMethod;
        string policy = EventStoreWorkloadAuthenticationDefaults.OperationPolicy(RouteCatalogEndpointExtensions.WorkloadScheme, EventStoreOperationIdentifiers.RouteCatalogRead);
        var receiver = new RouteCatalogReceiverSurface(RouteCatalogGeneratedRestPolicy.FenceVersion,
            [new(typed.Pattern, ["GET"], "generated-human-query", typed.Policies, false, true, true),
                new("/" + control, ["GET"], EventStoreOperationIdentifiers.RouteCatalogRead, [policy], false, false, true)])
        {
            GeneratedRest = scenario == "missing-typed" ? [] : [scenario == "tampered-typed" ? typed with { OperationDigest = new string('0', 64) } : typed],
        };
        string caller = scenario == "enrolled-control" ? "gateway" : "ui";
        string method = scenario is "control" or "enrolled-control" ? control : scenario == "service" ? "query" : "/api/probe/*";
        string verb = scenario is "post" or "service" ? "POST" : "GET";
        string digest = new('a', 64);
        RouteCatalogHostIdentity[] hosts = [new("generated-api", "generated-api", digest, digest, digest), new("gateway", "gateway", digest, digest, digest)];
        byte[] component = JsonSerializer.SerializeToUtf8Bytes(new
        {
            kind = "Component", spec = new { type = "state.redis", metadata = new[] { new { name = "keyPrefix", value = "none" } } },
            scopes = hosts.Select(host => host.AppId).ToArray(),
        });
        byte[] acl = JsonSerializer.SerializeToUtf8Bytes(new
        {
            spec = new { accessControl = new { defaultAction = "deny", trustDomain = "public",
                policies = new[] { new { appId = caller, defaultAction = "deny", operations = new[] { new { name = method, action = "allow", httpVerb = new[] { verb } } } } } } },
        });
        Action validate = () => RouteCatalogTopologyValidator.ValidateDevelopment(component, acl, catalog, hosts, "generated-api", [], receiver,
            ["generated-api", "gateway", "ui"], scenario == "undeclared" ? [] : [new(caller, method, [verb])]);
        if (admitted) Should.NotThrow(validate);
        else Should.Throw<RouteCatalogValidationException>(validate);
    }
}
