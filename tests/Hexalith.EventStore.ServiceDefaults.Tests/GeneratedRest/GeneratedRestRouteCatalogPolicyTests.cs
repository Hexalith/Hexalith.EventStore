using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Shouldly;

namespace Hexalith.EventStore.ServiceDefaults.Tests.GeneratedRest;

/// <summary>Independent owner-schema, topology admission and observation-identity checks for the opt-in family.</summary>
public sealed class GeneratedRestRouteCatalogPolicyTests
{
    /// <summary>Requires every reachable admitted endpoint to have the same full compiled public query identity.</summary>
    [Theory]
    [InlineData("valid", true)]
    [InlineData("bounded-prefix", true)]
    [InlineData("wildcard-only", false)]
    [InlineData("broad-prefix", false)]
    [InlineData("deep-catchall", false)]
    [InlineData("wrong-verb", false)]
    [InlineData("unknown-target", false)]
    [InlineData("missing-typed", false)]
    [InlineData("wrong-fence", false)]
    [InlineData("declaration-digest", false)]
    [InlineData("operation-digest", false)]
    [InlineData("request-type", false)]
    [InlineData("schema-change", false)]
    [InlineData("untyped-overlap", false)]
    [InlineData("anonymous", false)]
    [InlineData("missing-policy", false)]
    public void GrantsRequireActualTypedQueryInventory(string fault, bool expected)
    {
        ArgumentNullException.ThrowIfNull(fault);
        RouteCatalogEnvelope catalog = GeneratedRestProbeDeclarations.Catalog();
        var declaration = catalog.Declarations.Single(); var operation = catalog.Operations!.Single();
        string[] policies = [EventStoreHttpHostSecurityExtensions.HumanPolicy];
        var typed = new RouteCatalogGeneratedRestEndpoint("/api/probe/{tenantId}", declaration.RouteId,
            RouteCatalogCodec.ComputeDeclarationDigest(declaration), operation.Digest, typeof(GeneratedRestProbeQuery).FullName!, policies);
        var actual = new RouteCatalogReceiverEndpoint(typed.Pattern, ["GET"], "generated-human-query", policies, false, true, true);
        if (fault == "declaration-digest") typed = typed with { DeclarationDigest = new string('0', 64) };
        if (fault == "operation-digest") typed = typed with { OperationDigest = new string('0', 64) };
        if (fault == "request-type") typed = typed with { ContractType = typeof(string).FullName! };
        if (fault == "anonymous") actual = actual with { Anonymous = true };
        if (fault == "missing-policy") actual = actual with { Policies = ["same-looking-policy"] };
        if (fault == "schema-change")
        {
            var changed = operation with { RequestSchema = JsonSerializer.SerializeToElement(new { type = "string" }) };
            changed = changed with { Digest = RouteCatalogCodec.ComputeOperationContractDigest(changed) };
            catalog = catalog with { Operations = [changed] };
        }
        List<RouteCatalogReceiverEndpoint> endpoints = [actual];
        if (fault == "untyped-overlap") endpoints.Add(actual with { Pattern = "/api/probe/{tenantId}/untyped" });
        var receiver = new RouteCatalogReceiverSurface(fault == "wrong-fence" ? FiniteRouteCatalogReceiver.Version : RouteCatalogGeneratedRestPolicy.FenceVersion, endpoints)
        { GeneratedRest = fault == "missing-typed" ? [] : [typed] };
        string grant = fault switch
        {
            "valid" => "/api/probe/*", "wildcard-only" => "/**", "broad-prefix" => "/api/**",
            "deep-catchall" => "/api/probe/**", "unknown-target" => "/api/unrelated/*", _ => "/api/probe",
        };
        RouteCatalogGeneratedRestPolicy.AdmitsGrant(catalog, receiver, grant, [fault == "wrong-verb" ? "POST" : "GET"]).ShouldBe(expected);
    }

    /// <summary>The independently captured typed schema identity participates in the existing topology digest.</summary>
    [Fact]
    public void TypedObservationChangesCannotReuseAnExistingHostIdentity()
    {
        var catalog = GeneratedRestProbeDeclarations.Catalog(); var declaration = catalog.Declarations.Single();
        var typed = new RouteCatalogGeneratedRestEndpoint("/api/probe/{tenantId}", declaration.RouteId,
            RouteCatalogCodec.ComputeDeclarationDigest(declaration), catalog.Operations!.Single().Digest,
            typeof(GeneratedRestProbeQuery).FullName!, [EventStoreHttpHostSecurityExtensions.HumanPolicy]);
        var surface = new RouteCatalogReceiverSurface(RouteCatalogGeneratedRestPolicy.FenceVersion, []) { GeneratedRest = [typed] };
        var observation = new RouteCatalogHostObservation("generated-api", "generated-api", catalog.Declarations, [], ["generated-api"],
            [JsonSerializer.SerializeToUtf8Bytes(new { publicComponent = "fixture" }), JsonSerializer.SerializeToUtf8Bytes(new { publicAcl = "fixture" })],
            JsonSerializer.SerializeToUtf8Bytes(new { issuer = "generated-rest-fixture" }), [], [], [], ReceiverSurface: surface, Operations: catalog.Operations)
        { AuthorityCoordinates = new("catalog-state", "catalog-generation", "http://127.0.0.1:3500") };
        string original = ObservedRouteCatalogHost.Identity(observation).TopologyDigest;
        foreach (var changed in new[] { typed with { OperationDigest = new string('0', 64) }, typed with { ContractType = typeof(string).FullName! }, typed with { Pattern = "/api/another/{tenantId}" } })
            ObservedRouteCatalogHost.Identity(observation with { ReceiverSurface = surface with { GeneratedRest = [changed] } }).TopologyDigest.ShouldNotBe(original);
    }
}
