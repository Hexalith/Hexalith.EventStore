using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class TechnicalTopologyVerbIsolationTests
{
    [Fact]
    public async Task ActualPostCommandGrantDoesNotRequireItsWitnessOnGetStatusActions()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(CommandsController).Assembly);
        await using var app = builder.Build();
        app.MapControllers();
        string[] patterns = ["api/v1/commands", "api/v1/commands/status/{messageId}", "api/v1/commands/status/workload/{tenant}/{messageId}"];
        var grants = new[] { new RouteCatalogHttpGrant("/api/v1/commands", "POST"), new RouteCatalogHttpGrant("/api/v1/commands/status/**", "GET") };
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
            .Where(endpoint => patterns.Contains(endpoint.RoutePattern.RawText?.Trim('/'), StringComparer.Ordinal)).Select(endpoint =>
            {
                string[] methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.ToArray();
                var observed = DevelopmentTechnicalHttpTransport.Observe(endpoint).ShouldNotBeNull();
                var http = observed with { Grants = grants.Where(grant => DevelopmentTechnicalHttpTransport.GrantReaches(grant.Method, endpoint.RoutePattern.RawText!)
                    && methods.Any(method => grant.Verbs.Split(' ').Contains(method, StringComparer.Ordinal))).ToArray() };
                return new RouteCatalogReceiverEndpoint("/" + endpoint.RoutePattern.RawText!.Trim('/'), methods,
                    DevelopmentTechnicalHttpTransport.Operation(endpoint), endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy!).ToArray(),
                    false, endpoint.RoutePattern.Parameters.Count != 0, true) { Http = http };
            }).ToArray();
        endpoints.Length.ShouldBe(3);
        endpoints.Where(endpoint => endpoint.Methods.Contains("GET")).ShouldAllBe(endpoint => endpoint.Http!.Grants!.Single().Verbs == "GET");
        var fixture = new RouteCatalogTestFixture();
        var surface = new RouteCatalogReceiverSurface(FiniteRouteCatalogReceiver.TechnicalVersion, endpoints);
        Validate(fixture, grants, surface, DevelopmentTechnicalHttpTransport.Auxiliary(surface, fixture.Hosts[0].AppId, "sdk"));
    }

    [Theory]
    [InlineData("valid", null)]
    [InlineData("empty", "unqualified-technical-receiver-methods")]
    [InlineData("wildcard", "unqualified-technical-receiver-methods")]
    [InlineData("blank", "unqualified-technical-receiver-methods")]
    [InlineData("unknown", "unqualified-technical-receiver-methods")]
    [InlineData("duplicate", "unqualified-technical-receiver-methods")]
    [InlineData("different", "technical-receiver-method-evidence-drift")]
    [InlineData("additional", "technical-receiver-method-evidence-drift")]
    [InlineData("missing-witness", "unbound-technical-controller-code-or-policy")]
    [InlineData("code", "unbound-technical-controller-code-or-policy")]
    [InlineData("owner", "unbound-technical-controller-code-or-policy")]
    [InlineData("policy", "unbound-technical-controller-code-or-policy")]
    [InlineData("missing-auxiliary", "unbound-technical-controller-code-or-policy")]
    [InlineData("extra-grant-verb", "unqualified-technical-http-verb")]
    public void UnrelatedVerbFilteringCannotHideMalformedOrUnboundEvidence(string fault, string? refusal)
    {
        var fixture = new RouteCatalogTestFixture();
        var grants = new[] { new RouteCatalogHttpGrant("/api/v1/commands", fault == "extra-grant-verb" ? "POST PUT" : "POST"),
            new RouteCatalogHttpGrant("/api/v1/commands/status/**", "GET") };
        var root = Endpoint("/api/v1/commands", "POST", "Hexalith.EventStore.Controllers.CommandsController", grants[0]);
        var child = Endpoint("/api/v1/commands/status/{messageId}", "GET", "Hexalith.EventStore.Controllers.CommandStatusController", grants[1]);
        child = child with { Methods = fault switch { "empty" => [], "wildcard" => ["*"], "blank" => [""], "unknown" => ["CONNECT"], "duplicate" => ["GET", "GET"], _ => child.Methods } };
        if (fault is "different" or "additional") child = child with { Http = child.Http! with { Verbs = fault == "different" ? "POST" : "GET POST" } };
        var auxiliary = new[] { Auxiliary(root, fixture), Auxiliary(child, fixture) };
        if (fault == "missing-witness") root = root with { Http = root.Http! with { Grants = [] } };
        if (fault == "code") root = root with { Http = root.Http! with { CodeDigest = RouteCatalogTestFixture.Hash("changed-code") } };
        if (fault == "owner") root = root with { Http = root.Http! with { Owner = "foreign-controller" } };
        if (fault == "policy") root = root with { Policies = ["foreign-policy"] };
        if (fault == "missing-auxiliary") auxiliary = [auxiliary[1]];
        void Check() => Validate(fixture, grants, new(FiniteRouteCatalogReceiver.TechnicalVersion, [root, child]), auxiliary);
        if (refusal is null) Check();
        else Should.Throw<RouteCatalogValidationException>(Check).Diagnostics.Single().Code.ShouldBe(refusal);
    }

    [Fact]
    public void ExplicitMultiVerbTransportRetainsEveryGrantAndOwnerBinding()
    {
        var fixture = new RouteCatalogTestFixture();
        var grant = new RouteCatalogHttpGrant("/api/v1/commands", "GET POST");
        var endpoint = Endpoint("/api/v1/commands", "GET POST", "Hexalith.EventStore.Controllers.CommandsController", grant);
        Validate(fixture, [grant], new(FiniteRouteCatalogReceiver.TechnicalVersion, [endpoint]), [Auxiliary(endpoint, fixture)]);
    }

    private static RouteCatalogReceiverEndpoint Endpoint(string path, string verbs, string owner, RouteCatalogHttpGrant grant)
        => new(path, verbs.Split(' '), "sdk-http:" + path.Trim('/'), ["EventStoreHuman"], false, path.Contains('{'), true)
        { Http = new(owner, verbs, RouteCatalogTestFixture.Hash("compiled-code")) { Grants = [grant] } };

    private static RouteCatalogAuxiliaryOperation Auxiliary(RouteCatalogReceiverEndpoint endpoint, RouteCatalogTestFixture fixture)
        => new(fixture.Hosts[0].AppId, endpoint.Pattern.Trim('/'), endpoint.Operation!, "sdk") { Http = endpoint.Http };

    private static void Validate(RouteCatalogTestFixture fixture, IReadOnlyList<RouteCatalogHttpGrant> grants,
        RouteCatalogReceiverSurface surface, IReadOnlyList<RouteCatalogAuxiliaryOperation> auxiliary)
        => RouteCatalogTopologyValidator.ValidateDevelopment(fixture.Observations[0].TopologyDocuments[0], JsonSerializer.SerializeToUtf8Bytes(new
        {
            spec = new { accessControl = new { defaultAction = "deny", trustDomain = "public", policies = new[]
            { new { appId = fixture.Hosts[0].AppId, defaultAction = "deny", operations = grants.Select(grant => new { name = grant.Method, action = "allow", httpVerb = grant.Verbs.Split(' ') }) } } } },
        }), fixture.Envelope, fixture.Hosts, fixture.Hosts[0].AppId, auxiliary, surface);
}
