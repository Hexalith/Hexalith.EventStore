using System.Net.Http.Json;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
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

public sealed class CatalogLegacyProjectionResponseTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonemptyLegacyDeliveryAcceptsOnlyDeclaredDecodedOutputBeforeStateAndCheckpoint(bool foreign)
    {
        var fixture = new RouteCatalogTestFixture(); await fixture.ActivateAsync(fixture.Candidate());
        int httpEffects = 0; var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var sink = builder.Build();
        sink.MapPost("/v1.0/invoke/sample/method/project", async (HttpContext context) =>
        {
            httpEffects++; context.Request.Headers[RouteCatalogInvocationHandler.RootHeader].ToString().ShouldBe(fixture.Candidate().RootDigest);
            ProjectionRequest request = (await context.Request.ReadFromJsonAsync<ProjectionRequest>())!;
            request.Events.ShouldHaveSingleItem(); request.TenantId.ShouldBe("tenant-a");
            return Results.Json(new ProjectionResponse(foreign ? "foreign-output" : "counter", JsonSerializer.SerializeToElement(new { count = 17 })));
        });
        await sink.StartAsync(); using var dapr = new DaprClientBuilder().UseHttpEndpoint(sink.Urls.Single()).Build();
        using var client = new HttpClient(new RouteCatalogInvocationHandler(fixture.Authorities[0], new SocketsHttpHandler()));
        var clients = Substitute.For<IHttpClientFactory>(); clients.CreateClient(Arg.Any<string>()).Returns(client);
        var actors = Substitute.For<IActorProxyFactory>(); var aggregate = Substitute.For<IAggregateActor>();
        aggregate.GetEventsAsync(0).Returns([new EventEnvelope("01M61800000000000000000001", "aggregate", "counter", "tenant-a", "counter", 1, 1,
            DateTimeOffset.UtcNow, "correlation", "causation", "user", "v1", "CounterIncremented", 1, "json", "{\"amount\":17}"u8.ToArray(), null)]);
        actors.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(aggregate);
        var writeActor = Substitute.For<IProjectionWriteActor>(); var stored = new Dictionary<string, byte[]>();
        writeActor.UpdateProjectionAsync(Arg.Any<ProjectionState>()).Returns(c =>
        {
            var state = c.Arg<ProjectionState>(); stored[state.TenantId + ":" + state.ProjectionType] = state.StateBytes.ToArray(); return Task.CompletedTask;
        });
        actors.CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(writeActor);
        var etag = Substitute.For<IETagActor>(); etag.RegenerateAsync().Returns("etag");
        actors.CreateActorProxy<IETagActor>(Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(etag);
        long checkpoint = 0; var checkpoints = Substitute.For<IProjectionCheckpointTracker>();
        checkpoints.SaveDeliveredSequenceAsync(Arg.Any<Contracts.Identity.AggregateIdentity>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(c => { checkpoint = c.Arg<long>(); return true; });
        var resolver = Substitute.For<IDomainServiceResolver>();
        var orchestrator = new ProjectionUpdateOrchestrator(fixture.Authorities[0], actors, dapr, clients, resolver, checkpoints,
            Options.Create(new ProjectionOptions()), NullLogger<ProjectionUpdateOrchestrator>.Instance);
        await orchestrator.DeliverProjectionAsync(new("tenant-a", "counter", "aggregate"));
        httpEffects.ShouldBe(1); resolver.ReceivedCalls().ShouldBeEmpty(); checkpoint.ShouldBe(foreign ? 0 : 1);
        if (foreign) stored.ShouldBeEmpty();
        else { stored.Keys.ShouldBe(["tenant-a:counter"]); JsonSerializer.Deserialize<JsonElement>(stored["tenant-a:counter"]).GetProperty("count").GetInt32().ShouldBe(17); }
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }
}
