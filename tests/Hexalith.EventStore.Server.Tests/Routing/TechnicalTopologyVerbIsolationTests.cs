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
        string[] patterns = ["api/v1/commands", "api/v1/commands/status/{messageId}"];
        var grants = new[] { new RouteCatalogHttpGrant("/api/v1/commands", "POST"), new RouteCatalogHttpGrant("/api/v1/commands/status/*", "GET") };
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
        endpoints.Length.ShouldBe(2);
        var workload = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText == "api/v1/commands/status/workload/{tenant}/{messageId}");
        DevelopmentTechnicalHttpTransport.Observe(workload).ShouldBeNull();
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
    [InlineData("anonymous", "unbound-technical-controller-code-or-policy")]
    [InlineData("auxiliary-subset", "unbound-technical-controller-code-or-policy")]
    [InlineData("missing-auxiliary", "unbound-technical-controller-code-or-policy")]
    [InlineData("extra-grant-verb", "unqualified-technical-http-verb")]
    public void UnrelatedVerbFilteringCannotHideMalformedOrUnboundEvidence(string fault, string? refusal)
    {
        var fixture = new RouteCatalogTestFixture();
        var grants = new[] { new RouteCatalogHttpGrant("/api/v1/commands", fault == "extra-grant-verb" ? "POST PUT" : "POST") };
        var statusGrant = new RouteCatalogHttpGrant("/api/v1/commands/status/*", "GET");
        var root = Endpoint("/api/v1/commands", "POST", "Hexalith.EventStore.Controllers.CommandsController", grants[0]);
        var child = Endpoint("/api/v1/commands/status/{messageId}", "GET", "Hexalith.EventStore.Controllers.CommandStatusController", statusGrant);
        var auxiliary = new[] { Auxiliary(root, fixture), Auxiliary(child, fixture) };
        child = child with { Methods = fault switch { "empty" => [], "wildcard" => ["*"], "blank" => [""], "unknown" => ["CONNECT"], "duplicate" => ["GET", "GET"], _ => child.Methods } };
        if (fault is "different" or "additional") child = child with { Http = child.Http! with { Verbs = fault == "different" ? "POST" : "GET POST" } };
        if (fault == "missing-witness") child = child with { Http = child.Http! with { Grants = [] } };
        if (fault == "code") child = child with { Http = child.Http! with { CodeDigest = RouteCatalogTestFixture.Hash("changed-code") } };
        if (fault == "owner") child = child with { Http = child.Http! with { Owner = "foreign-controller" } };
        if (fault == "policy") child = child with { Policies = ["foreign-policy"] };
        if (fault == "anonymous") child = child with { Anonymous = true };
        if (fault == "missing-auxiliary") auxiliary = [auxiliary[0]];
        if (fault == "auxiliary-subset") auxiliary[1] = auxiliary[1] with { Http = auxiliary[1].Http! with { Verbs = "GET POST" } };
        void Check() => Validate(fixture, grants, new(FiniteRouteCatalogReceiver.TechnicalVersion, [root, child]), auxiliary);
        if (refusal is null) Check();
        else Should.Throw<RouteCatalogValidationException>(Check).Diagnostics.Single().Code.ShouldBe(refusal);
    }

    [Fact]
    public void MutatedEndpointMethodsCannotHideContradictoryAuxiliaryEvidence()
    {
        var fixture = new RouteCatalogTestFixture();
        var grant = new RouteCatalogHttpGrant("/api/v1/commands", "POST");
        var root = Endpoint("/api/v1/commands", "POST", "Hexalith.EventStore.Controllers.CommandsController", grant);
        var child = Endpoint("/api/v1/commands/status/{messageId}", "POST", "Hexalith.EventStore.Controllers.CommandStatusController", grant);
        var auxiliary = new[] { Auxiliary(root, fixture), Auxiliary(child, fixture) };
        child = child with { Methods = ["GET"], Http = child.Http! with { Verbs = "GET" } };
        Should.Throw<RouteCatalogValidationException>(() => Validate(fixture, [grant],
            new(FiniteRouteCatalogReceiver.TechnicalVersion, [root, child]), auxiliary));
    }

    [Fact]
    public void ExplicitMultiVerbTransportRetainsEveryGrantAndOwnerBinding()
    {
        var fixture = new RouteCatalogTestFixture();
        var grant = new RouteCatalogHttpGrant("/api/v1/commands", "GET POST");
        var endpoint = Endpoint("/api/v1/commands", "GET POST", "Hexalith.EventStore.Controllers.CommandsController", grant);
        Validate(fixture, [grant], new(FiniteRouteCatalogReceiver.TechnicalVersion, [endpoint]), [Auxiliary(endpoint, fixture)]);
    }

    [Fact]
    public void SeparateMvcActionsRetainTheCompleteAggregatedAuxiliaryMethodSet()
    {
        var fixture = new RouteCatalogTestFixture();
        var grant = new RouteCatalogHttpGrant("*", "POST");
        var post = Endpoint("/api/v1/admin/tenants", "POST", "Hexalith.EventStore.Controllers.AdminTenantsController", grant);
        var get = Endpoint("/api/v1/admin/tenants", "GET", "Hexalith.EventStore.Controllers.AdminTenantsController",
            new RouteCatalogHttpGrant("*", "GET"));
        var surface = new RouteCatalogReceiverSurface(FiniteRouteCatalogReceiver.TechnicalVersion, [post, get]);
        var auxiliary = DevelopmentTechnicalHttpTransport.Auxiliary(surface, fixture.Hosts[0].AppId, "sdk");
        auxiliary.Single().Http!.Verbs.ShouldBe("GET POST");
        Validate(fixture, [grant], surface, auxiliary);
    }

    [Fact]
    public void SplitConcreteGrantsUseTheCompleteCapturedOperationMethodSet()
    {
        var fixture = new RouteCatalogTestFixture();
        var getGrant = new RouteCatalogHttpGrant("/api/items/one", "GET");
        var postGrant = new RouteCatalogHttpGrant("/api/items/two", "POST");
        var get = Endpoint("/api/items/{id}", "GET", "SdkController", getGrant);
        var post = Endpoint("/api/items/{id}", "POST", "SdkController", postGrant);
        var surface = new RouteCatalogReceiverSurface(FiniteRouteCatalogReceiver.TechnicalVersion, [get, post]);
        var auxiliary = DevelopmentTechnicalHttpTransport.Auxiliary(surface, fixture.Hosts[0].AppId, "sdk");
        auxiliary.Single().Http!.Verbs.ShouldBe("GET POST");
        Validate(fixture, [getGrant, postGrant], surface, auxiliary);
    }

    [Theory]
    [InlineData("native", "valid")]
    [InlineData("native", "methods")]
    [InlineData("native", "verbs")]
    [InlineData("native", "kind")]
    [InlineData("native", "code")]
    [InlineData("native", "framework")]
    [InlineData("native", "inventory")]
    [InlineData("native", "policy")]
    [InlineData("native", "anonymous")]
    [InlineData("native", "auxiliary")]
    [InlineData("callback", "valid")]
    [InlineData("callback", "methods")]
    [InlineData("callback", "verbs")]
    [InlineData("callback", "kind")]
    [InlineData("callback", "code")]
    [InlineData("callback", "framework")]
    [InlineData("callback", "inventory")]
    [InlineData("callback", "policy")]
    [InlineData("callback", "anonymous")]
    [InlineData("callback", "auxiliary")]
    public void ExcludedNativeAndCallbackReceiversRetainEveryIntrinsicGuard(string kind, string fault)
    {
        var fixture = new RouteCatalogTestFixture();
        var grant = new RouteCatalogHttpGrant("*", "POST");
        var root = Endpoint("/api/v1/commands", "POST", "Hexalith.EventStore.Controllers.CommandsController", grant);
        string digest = RouteCatalogTestFixture.Hash("compiled-code");
        var child = new RouteCatalogReceiverEndpoint(kind == "native" ? "/actors/config" : "/dapr/subscribe",
            ["GET"], "sdk-callback", ["EventStoreSidecarChannel"], false, false, true)
        {
            NativeActor = kind == "native" ? new("config", "GET", digest, digest, digest) : null,
            DaprCallback = kind == "callback" ? new("subscribe", "GET", digest, digest, digest) : null,
        };
        var auxiliary = new[] { Auxiliary(root, fixture), new RouteCatalogAuxiliaryOperation(fixture.Hosts[0].AppId,
            child.Pattern.Trim('/'), child.Operation!, "sdk") { NativeActor = child.NativeActor, DaprCallback = child.DaprCallback } };
        if (fault == "methods") child = child with { Methods = ["PUT"] };
        if (fault == "policy") child = child with { Policies = ["foreign-policy"] };
        if (fault == "anonymous") child = child with { Anonymous = true };
        if (fault == "auxiliary") auxiliary = [auxiliary[0]];
        if (child.NativeActor is { } native)
            child = child with { NativeActor = native with {
                Kind = fault == "kind" ? "unknown" : native.Kind,
                Verbs = fault == "verbs" ? "PUT" : native.Verbs,
                CodeDigest = fault == "code" ? "invalid" : native.CodeDigest,
                FrameworkDigest = fault == "framework" ? "invalid" : native.FrameworkDigest,
                InventoryDigest = fault == "inventory" ? "invalid" : native.InventoryDigest } };
        if (child.DaprCallback is { } callback)
            child = child with { DaprCallback = callback with {
                Kind = fault == "kind" ? "unknown" : callback.Kind,
                Verbs = fault == "verbs" ? "POST" : callback.Verbs,
                CodeDigest = fault == "code" ? "invalid" : callback.CodeDigest,
                FrameworkDigest = fault == "framework" ? "invalid" : callback.FrameworkDigest,
                InventoryDigest = fault == "inventory" ? "invalid" : callback.InventoryDigest } };
        void Check() => Validate(fixture, [grant], new(FiniteRouteCatalogReceiver.TechnicalVersion, [root, child]), auxiliary);
        if (fault == "valid") Check();
        else Should.Throw<RouteCatalogValidationException>(Check).Diagnostics.Single().Code
            .ShouldBe(kind == "native" ? "unbound-native-actor-receiver" : "unbound-sdk-dapr-callback-receiver");
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
        }), CatalogSchemaThreeFixture.Create(fixture.Envelope), fixture.Hosts, fixture.Hosts[0].AppId, auxiliary, surface);
}
