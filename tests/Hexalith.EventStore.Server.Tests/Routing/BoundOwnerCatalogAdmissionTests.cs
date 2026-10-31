using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Extensions;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using MediatR;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Public UI-only metadata preserves only the existing signed, allowlisted owner-query protocol.</summary>
public sealed class BoundOwnerCatalogAdmissionTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("signature")]
    [InlineData("unconfigured-caller")]
    [InlineData("actor")]
    [InlineData("tenant")]
    [InlineData("query")]
    [InlineData("payload")]
    [InlineData("missing-surface")]
    [InlineData("human-surface")]
    [InlineData("copied-marker")]
    [InlineData("mutated-actor")]
    [InlineData("mutated-tenant")]
    [InlineData("mutated-query")]
    [InlineData("mutated-payload")]
    [InlineData("foreign-route")]
    [InlineData("stale-authority")]
    public async Task ControllerProofBindsExactInFlightQueryBeforeRealMediatorEligibility(string scenario)
    {
        var original = new RouteCatalogTestFixture().Envelope;
        var operations = original.Declarations.Select(declaration => new RouteOperationContract(declaration.RouteId, declaration.ContractVersion,
            declaration.Kind == "projection" || declaration.Execution!.RequiresDelegatedContext ? "internal" : "public", "UI-only",
            JsonSerializer.SerializeToElement(new { type = "object" }), JsonSerializer.SerializeToElement(new { type = "object" }), string.Empty,
            declaration.Kind, declaration.Domain, declaration.MessageType ?? declaration.ProjectionType))
            .Select(operation => operation with { Digest = RouteCatalogCodec.ComputeOperationContractDigest(operation) }).ToArray();
        var envelope = VersionedRouteCatalogBuilder.Build(new(1, "Development", original.Declarations, operations,
            original.Routes.Select(route => new RouteCompositionBinding(route.RouteId, route.AppId, route.Method, route.ContractVersion, false, route.ResolverTenantPattern)).ToArray(), original.Idempotency));
        byte[] canonical = RouteCatalogCodec.Encode(envelope);
        string root = RouteCatalogCodec.ComputeRootDigest(envelope);
        var declaration = envelope.Declarations.First(route => route.Kind == "query" && operations.Single(operation => operation.RouteId == route.RouteId).Ingress == "public");
        using RSA key = RSA.Create(2048);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:BoundStreamRead:PublicKeys:owner"] = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
            ["Authentication:BoundOwnerQuery:AllowedQueries:owner"] = declaration.MessageType,
        }).Build();
        int leases = 0;
        long epoch = 1;
        IRouteCatalogAuthority authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        { leases++; return new RouteCatalogDispatchLease(new(canonical, root, epoch), () => { leases--; return Task.CompletedTask; }); });
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddEventStore();
        services.AddHttpContextAccessor();
        services.AddSingleton(authority);
        services.RemoveAll<IRequestHandler<SubmitQuery, SubmitQueryResult>>();
        var terminal = new BoundOwnerCatalogQueryHandler(() => leases >= 2);
        services.AddSingleton<IRequestHandler<SubmitQuery, SubmitQueryResult>>(terminal);
        await using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IHttpContextAccessor>();
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(QueriesController).Assembly);
        await using var app = builder.Build(); app.MapControllers();
        var endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText == "api/v1/queries/owner-authority/workload");
        var context = new DefaultHttpContext();
        context.SetEndpoint(endpoint);
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/queries/owner-authority/workload";
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "workload:owner"),
            new(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, scenario == "unconfigured-caller" ? "foreign" : "owner"),
            new(EventStoreWorkloadAuthenticationDefaults.OperationClaimType, EventStoreWorkloadOperations.GatewayOwnerAuthorityQuery) };
        if (scenario != "missing-surface") claims.Add(new(RouteCatalogSurfacePolicy.ClaimType, scenario == "human-surface" ? "ui" : "service"));
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "validated-workload"));
        accessor.HttpContext = context;
        IMediator realMediator = provider.GetRequiredService<IMediator>();
        IMediator bridge = Substitute.For<IMediator>();
        SubmitQuery? dispatched = null;
        bridge.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var query = call.Arg<SubmitQuery>();
            dispatched = query;
            if (scenario == "mutated-actor") query = query with { OriginalActorId = "foreign-actor" };
            if (scenario == "mutated-tenant") query = query with { Tenant = "foreign-tenant" };
            if (scenario == "mutated-query") query = query with { QueryType = "foreign-query" };
            if (scenario == "mutated-payload") query.Payload[0] ^= 1;
            if (scenario == "stale-authority") epoch = 2;
            if (scenario == "foreign-route") context.Request.Path = "/api/v1/queries";
            if (scenario == "copied-marker")
            {
                var copied = new DefaultHttpContext { User = context.User };
                copied.SetEndpoint(endpoint); copied.Request.Method = context.Request.Method; copied.Request.Path = context.Request.Path;
                foreach (var item in context.Items) copied.Items[item.Key] = item.Value;
                accessor.HttpContext = copied;
            }
            return realMediator.Send(query, call.Arg<CancellationToken>());
        });
        var controller = new QueriesController(authority, bridge, Substitute.For<IETagService>(), new ClaimsTenantValidator(), new ClaimsRbacValidator(),
            NullLogger<QueriesController>.Instance, configuration: configuration) { ControllerContext = new() { HttpContext = context } };
        var request = new BoundOwnerQueryRequest(new SubmitQueryRequest("tenant-a", declaration.Domain, "aggregate-one", declaration.MessageType!,
            Payload: JsonSerializer.SerializeToElement(new { selector = "one" })), "actor-one");
        context.Request.Headers[BoundOwnerQueryProof.HeaderName] = BoundOwnerQueryProof.Issue(request, "owner", Convert.ToBase64String(key.ExportPkcs8PrivateKey()), DateTimeOffset.UtcNow);
        request = scenario switch
        {
            "actor" => request with { PrincipalId = "different-actor" },
            "tenant" => request with { Query = request.Query with { Tenant = "different-tenant" } },
            "query" => request with { Query = request.Query with { QueryType = "different-query" } },
            "payload" => request with { Query = request.Query with { Payload = JsonSerializer.SerializeToElement(new { selector = "other" }) } },
            _ => request,
        };
        if (scenario == "signature") context.Request.Headers[BoundOwnerQueryProof.HeaderName] = "invalid";
        if (scenario is "copied-marker" or "mutated-actor" or "mutated-tenant" or "mutated-query" or "mutated-payload" or "foreign-route" or "stale-authority")
            await Should.ThrowAsync<CommandAuthorizationException>(() => controller.SubmitBoundOwnerAuthorityAsync(request, default));
        else
        {
            var response = await controller.SubmitBoundOwnerAuthorityAsync(request, default);
            if (scenario == "valid") response.ShouldBeOfType<OkObjectResult>();
            else response.ShouldBeOfType<ForbidResult>();
        }
        leases.ShouldBe(0);
        terminal.Calls.ShouldBe(scenario == "valid" ? 1 : 0);
        if (dispatched is not null)
        {
            accessor.HttpContext = context;
            await Should.ThrowAsync<CommandAuthorizationException>(() => realMediator.Send(dispatched));
            leases.ShouldBe(0);
        }
    }
}
