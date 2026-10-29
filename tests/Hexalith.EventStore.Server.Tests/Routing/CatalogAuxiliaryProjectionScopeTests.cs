using System.Net.Http.Json;

using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Client.Projections;

using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogAuxiliaryProjectionScopeTests
{
    [Theory]
    [InlineData("matching", false)]
    [InlineData("tenant", false)]
    [InlineData("output", false)]
    [InlineData("version", false)]
    [InlineData("matching", true)]
    [InlineData("tenant", true)]
    [InlineData("output", true)]
    public async Task DirectMappedReplayAndMultipartUploadsRequireExactTenantAndCallableOutputsBeforeHttp(string scope, bool multipart)
    {
        var fixture = new RouteCatalogTestFixture();
        var envelope = RouteCatalogCodec.Seal(fixture.Envelope with
        {
            Routes = fixture.Envelope.Routes.Select(r => r.RouteId == "counter-projection"
                ? r with { Method = "project/v2", ResolverTenantPattern = "tenant-a" } : r).ToArray(),
        });
        string fingerprint = RouteCatalogTestFixture.Hash("named-capability-v1");
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 1,
            auxiliaryOperations: [.. new[] { "commit", "stage", "part" }.Select(action => new RouteCatalogAuxiliaryOperation("sample", "project/rebuild/" + action + "/v1", EventStoreOperationIdentifiers.DomainServiceProject, "counter"))],
            projectionCapabilities: [new("sample", "counter", "v1", fingerprint, scope == "output" ? ["counter", "foreign-output"] : ["counter"])], localAppId: "eventstore");
        var authority = Substitute.For<IRouteCatalogAuthority>(); int leases = 0;
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ => { leases++; return new RouteCatalogDispatchLease(snapshot, () => { leases--; return Task.CompletedTask; }); });
        int effects = 0, parts = 0; var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var sink = builder.Build();
        sink.MapPost("/v1.0/invoke/sample/method/project/rebuild/{action}/v1", async (HttpContext context, string action) =>
        {
            effects++; if (action == "part") { parts++; return Results.NoContent(); }
            var request = (await context.Request.ReadFromJsonAsync<ProjectionDispatchRequest>())!;
            request.Request.TenantId.ShouldBe("tenant-a"); request.ProjectionTypes.ShouldBe(["counter"]);
            return Results.Json(new ProjectionDispatchResponse(2, [new("counter", ProjectionDispatchStatus.Completed, null, null)]));
        });
        await sink.StartAsync(); using var dapr = new DaprClientBuilder().UseHttpEndpoint(sink.Urls.Single()).Build();
        using var client = new HttpClient(new RouteCatalogInvocationHandler(authority, new SocketsHttpHandler()));
        var clients = Substitute.For<IHttpClientFactory>(); clients.CreateClient(Arg.Any<string>()).Returns(client);
        string[] requested = scope == "output" ? ["foreign-output"] : ["counter"];
        var cache = new NamedProjectionRouteCatalog();
        cache.Replace(new([new("sample", "v1", "counter", 2, "fixture-capability", fingerprint, requested, requested, requested)]));
        var coordinator = new NamedProjectionDispatchCoordinator(authority, cache, Substitute.For<IProjectionDeliveryCheckpointStore>(),
            Substitute.For<IProjectionLifecycleGateway>(), Substitute.For<IActorProxyFactory>(), dapr, clients,
            Options.Create(new ProjectionDispatchOptions()), NullLogger<NamedProjectionDispatchCoordinator>.Instance);
        string tenant = scope == "tenant" ? "tenant-b" : "tenant-a";
        var identity = new Contracts.Identity.AggregateIdentity(tenant, "counter", "aggregate");
        var registration = new Server.DomainServices.DomainServiceRegistration("sample", "process", tenant, "counter", scope == "version" ? "v99" : "v1");
        async Task ExecuteAsync()
        {
            if (multipart)
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes("[\"" + new string('x', ProjectionRebuildTransportReference.PartBytes + 100) + "\"]");
                var stage = await coordinator.StageMappedOnlyStreamedRebuildAsync(identity, registration, "operation", 1,
                    async (stream, token) => { await stream.WriteAsync(bytes, token); return null; }, default);
                stage.Result.Succeeded.ShouldBeTrue();
            }
            else
            {
                var header = new ProjectionDispatchRequest(new(tenant, "counter", "aggregate", []), requested, "operation", fingerprint)
                { RebuildTransport = new(1, RouteCatalogTestFixture.Hash("source")) };
                (await coordinator.InvokeMappedOnlyStreamedRebuildAsync(identity, registration, header, ProjectionMappedReplayAction.Commit, default)).Succeeded.ShouldBeTrue();
            }
        }
        if (scope == "matching") await ExecuteAsync();
        else await Should.ThrowAsync<RouteCatalogUnavailableException>(ExecuteAsync);
        effects.ShouldBe(scope == "matching" ? multipart ? 3 : 1 : 0); parts.ShouldBe(scope == "matching" && multipart ? 2 : 0);
        leases.ShouldBe(0);
    }
}
