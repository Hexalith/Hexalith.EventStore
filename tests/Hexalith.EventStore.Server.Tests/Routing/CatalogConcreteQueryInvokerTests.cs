using System.Net.Http.Json;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Queries;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogConcreteQueryInvokerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcreteHttpInvokerUsesCommittedTargetDespiteConflictingResolver(bool undeclared)
    {
        var fixture = new RouteCatalogTestFixture();
        const string committedMethod = "catalog-query-value";
        var declaration = fixture.Envelope.Declarations.First(d => d.Kind == "query" && d.Execution!.TargetKind == "domain-service" && !d.Execution.RequiresDelegatedContext);
        var envelope = RouteCatalogCodec.Seal(fixture.Envelope with
        {
            Routes = fixture.Envelope.Routes.Select(route => route.RouteId == declaration.RouteId ? route with { Method = committedMethod } : route).ToArray(),
        });
        for (int index = 0; index < fixture.Observations.Length; index++)
            fixture.Observations[index] = fixture.Observations[index] with
            {
                Targets = fixture.Observations[index].Targets.Select(route => route.RouteId == declaration.RouteId ? route with { Method = committedMethod } : route).ToArray(),
            };
        var hosts = fixture.Observations.Select(ObservedRouteCatalogHost.Identity).ToArray();
        var authorities = hosts.Select(host => fixture.CreateAuthority(host)).ToArray();
        var candidate = fixture.Candidate() with
        {
            CanonicalCatalog = RouteCatalogCodec.Encode(envelope), RootDigest = RouteCatalogCodec.ComputeRootDigest(envelope), RequiredHosts = hosts,
        };
        candidate = candidate with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(candidate) };
        await authorities[0].PrepareAsync(candidate);
        foreach (var authority in authorities) await authority.ReadyAsync(candidate.AttemptId);
        await authorities[0].CommitAsync(candidate.AttemptId);
        var snapshotLease = await authorities[0].AcquireAsync();
        var snapshot = snapshotLease.Snapshot; await snapshotLease.DisposeAsync();
        var target = snapshot.Envelope.Routes.Single(r => r.RouteId == declaration.RouteId);
        target.Method.ShouldNotBe(DaprDomainQueryInvoker.QueryMethodName);
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var sink = builder.Build(); int effects = 0;
        sink.MapPost($"/v1.0/invoke/{target.AppId}/method/{target.Method}", async (HttpContext context) =>
        {
            effects++; context.Request.Headers[RouteCatalogInvocationHandler.RootHeader].ToString().ShouldBe(snapshot.RootDigest);
            context.Request.Headers[RouteCatalogInvocationHandler.EpochHeader].ToString().ShouldBe("1");
            var request = await context.Request.ReadFromJsonAsync<QueryEnvelope>(); request!.Domain.ShouldBe(declaration.Domain);
            return Results.Json(QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { value = 7 }), declaration.ProjectionType));
        });
        await sink.StartAsync();
        using var dapr = new DaprClientBuilder().UseHttpEndpoint(sink.Urls.Single()).Build();
        using var client = new HttpClient(new RouteCatalogInvocationHandler(authorities[0], new SocketsHttpHandler()));
        var clients = Substitute.For<IHttpClientFactory>(); clients.CreateClient(Arg.Any<string>()).Returns(client);
        var resolver = Substitute.For<IDomainServiceResolver>();
        resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DomainServiceRegistration("conflicting-app", "conflicting-method", "tenant-a", declaration.Domain, "v99"));
        var invoker = new DaprDomainQueryInvoker(authorities[0], dapr, clients, resolver, NullLogger<DaprDomainQueryInvoker>.Instance);
        var query = new QueryEnvelope(target.ResolverTenantPattern is null or "*" ? "tenant-a" : target.ResolverTenantPattern, declaration.Domain, "aggregate", undeclared ? "not-declared" : declaration.MessageType!, [], "correlation", "user");
        if (undeclared) await Should.ThrowAsync<RouteCatalogUnavailableException>(() => invoker.InvokeAsync(query));
        else (await invoker.InvokeAsync(query)).GetPayload().GetProperty("value").GetInt32().ShouldBe(7);
        effects.ShouldBe(undeclared ? 0 : 1); resolver.ReceivedCalls().ShouldBeEmpty();
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActivatedActorResponseMustMatchBeforePayloadOrFurtherLifecycleProcessing(bool mismatch)
    {
        var fixture = new RouteCatalogTestFixture(); await fixture.ActivateAsync(fixture.Candidate());
        var actor = Substitute.For<IProjectionActorInvoker>();
        actor.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResult(true, "{\"value\":7}"u8.ToArray(), null, mismatch ? "different-projection" : "counter"));
        var lifecycle = Substitute.For<IProjectionLifecycleGateway>();
        var router = new QueryRouter(actor, NullLogger<QueryRouter>.Instance, lifecycle, catalogAuthority: fixture.Authorities[0]);
        var query = new SubmitQuery("tenant-a", "counter", "aggregate", "get-counter-status", [], "correlation", "user");
        if (mismatch)
        {
            var refused = await router.RouteQueryAsync(query);
            refused.Success.ShouldBeFalse(); refused.ErrorMessage.ShouldBe(QueryAdapterFailureReason.QueryExecutionFailure);
            refused.Payload.ShouldBeNull(); refused.Metadata.ShouldBeNull();
            lifecycle.ReceivedCalls().Count().ShouldBe(1);
        }
        else
        {
            var result = await router.RouteQueryAsync(query);
            result.Success.ShouldBeTrue(); result.Payload!.Value.GetProperty("value").GetInt32().ShouldBe(7);
        }
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }
}
