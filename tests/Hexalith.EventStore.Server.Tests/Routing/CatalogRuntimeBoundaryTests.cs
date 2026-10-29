using System.Net;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Testing.Http;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogRuntimeBoundaryTests
{
    [Theory]
    [InlineData("declared")]
    [InlineData("redirect")]
    [InlineData("raw-actor")]
    public async Task ActualHttpTransportCannotFollowRedirectsOrInvokeUnscopedActors(string scenario)
    {
        var fixture = new RouteCatalogTestFixture(); await fixture.ActivateAsync(fixture.Candidate());
        var target = fixture.Envelope.Routes.First(r => r.Method == "query");
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build(); int declared = 0, undeclared = 0;
        string normal = $"/v1.0/invoke/{target.AppId}/method/{target.Method}";
        app.MapPost(normal, () => { declared++; return scenario == "redirect"
            ? Results.Redirect("/v1.0/invoke/undeclared/method/unknown", permanent: false, preserveMethod: true) : Results.NoContent(); });
        app.MapPost("/v1.0/invoke/undeclared/method/unknown", () => { undeclared++; return Results.NoContent(); });
        app.MapPut("/v1.0/actors/UndeclaredActor/id/method/Unknown", () => { undeclared++; return Results.NoContent(); });
        await app.StartAsync();
        var services = new ServiceCollection(); services.AddSingleton<IRouteCatalogAuthority>(fixture.Authorities[0]);
        services.AddEventStoreRouteCatalog(new ConfigurationBuilder().Build()); services.AddHttpClient("probe");
        using var provider = services.BuildServiceProvider(); using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("probe");
        using var request = new HttpRequestMessage(scenario == "raw-actor" ? HttpMethod.Put : HttpMethod.Post,
            app.Urls.Single() + (scenario == "raw-actor" ? "/v1.0/actors/UndeclaredActor/id/method/Unknown" : normal));
        if (scenario == "raw-actor") await Should.ThrowAsync<RouteCatalogUnavailableException>(() => client.SendAsync(request));
        else { using var response = await client.SendAsync(request); response.StatusCode.ShouldBe(scenario == "redirect" ? HttpStatusCode.TemporaryRedirect : HttpStatusCode.NoContent); }
        declared.ShouldBe(scenario == "raw-actor" ? 0 : 1); undeclared.ShouldBe(0);
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicAndTypeBasedQueryRoutersRequireAuthorityBeforeActorEffects(bool typeBased)
    {
        var fixture = new RouteCatalogTestFixture(); await fixture.ActivateAsync(fixture.Candidate());
        var actors = Substitute.For<IActorProxyFactory>();
        var services = new ServiceCollection(); services.AddSingleton(actors); services.AddLogging();
        services.AddSingleton<IRouteCatalogAuthority>(fixture.Authorities[0]); services.AddTransient<IQueryRouter, QueryRouter>();
        using var provider = services.BuildServiceProvider();
        IQueryRouter router = typeBased ? provider.GetRequiredService<IQueryRouter>() : new QueryRouter(actors, NullLogger<QueryRouter>.Instance, fixture.Authorities[0]);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => router.RouteQueryAsync(new("tenant-a", "counter", "aggregate", "undeclared", [], "correlation", "user")));
        actors.ReceivedCalls().ShouldBeEmpty();
        typeof(QueryRouter).GetConstructors().ShouldHaveSingleItem().GetParameters().ShouldContain(p => p.ParameterType == typeof(IRouteCatalogAuthority));
        Should.Throw<ArgumentNullException>(() => new QueryRouter(actors, NullLogger<QueryRouter>.Instance, null!));
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CommandLocalFactoryFailuresDrainWhileActualActorFailuresRetain(bool fenced, bool remote)
    {
        var fixture = new RouteCatalogTestFixture(); var first = fixture.Candidate(); await fixture.ActivateAsync(first);
        var actors = Substitute.For<IActorProxyFactory>(); var actor = Substitute.For<IAggregateActor>();
        if (remote)
        {
            actors.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(actor);
            actor.ProcessCommandAsync(Arg.Any<CommandEnvelope>()).Returns(Task.FromException<CommandProcessingResult>(new IOException("transport failure")));
            actor.ProcessFencedCommandAsync(Arg.Any<FencedCommandEnvelope>()).Returns(Task.FromException<CommandProcessingResult>(new IOException("transport failure")));
        }
        else actors.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(_ => throw new InvalidOperationException("local proxy construction"));
        var command = new SubmitCommand("01M61800000000000000000001", "tenant-a", "counter", "counter-1", "increment-counter", "{}"u8.ToArray(), "correlation", "user");
        var context = new IdempotencyExecutionContext(1, "admission", 1, "v1", command.MessageId, command.CorrelationId, command.Tenant, command.Domain,
            command.AggregateId, command.CommandType, "proof");
        var router = new CommandRouter(fixture.Authorities[0], actors, Options.Create(new EventStoreActorOptions()), NullLogger<CommandRouter>.Instance);
        await Should.ThrowAsync<Exception>(() => fenced ? router.RouteFencedCommandAsync(command, context) : router.RouteCommandAsync(command));
        (await fixture.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(remote ? 1 : 0);
        if (!remote) actor.ReceivedCalls().ShouldBeEmpty();
        var next = fixture.Candidate(2, first.RootDigest); await fixture.Authorities[0].PrepareAsync(next);
        if (remote) await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].ReadyAsync(next.AttemptId));
        else await fixture.Authorities[0].ReadyAsync(next.AttemptId);
    }

    [Theory]
    [InlineData("StageReplayAsync", false)]
    [InlineData("StageReplayAsync", true)]
    [InlineData("PromoteReplayAsync", false)]
    [InlineData("PromoteReplayAsync", true)]
    [InlineData("FinalizeReplayAsync", false)]
    [InlineData("FinalizeReplayAsync", true)]
    [InlineData("ReadReplayCompletionAsync", false)]
    [InlineData("ReadReplayCompletionAsync", true)]
    public async Task ReplayFactoriesStayOutsideActualRemoteEnrollment(string method, bool remote)
    {
        var fixture = new RouteCatalogTestFixture(); await fixture.ActivateAsync(fixture.Candidate());
        var actors = Substitute.For<IActorProxyFactory>(); var actor = Substitute.For<IProjectionReplayPublicationActor>();
        if (remote)
        {
            actors.CreateActorProxy<IProjectionReplayPublicationActor>(Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(actor);
            actor.StageReplayAsync(Arg.Any<ProjectionReplayPublicationRequest>()).Returns(Task.FromException<ProjectionRebuildInspection>(new IOException()));
            actor.PromoteReplayAsync(Arg.Any<ProjectionReplayPublicationRequest>()).Returns(Task.FromException<ProjectionRebuildInspection>(new IOException()));
            actor.FinalizeReplayAsync(Arg.Any<ProjectionReplayPublicationRequest>()).Returns(Task.FromException<bool>(new IOException()));
            actor.ReadReplayCompletionAsync(Arg.Any<ProjectionReplayPublicationRequest>()).Returns(Task.FromException<bool>(new IOException()));
        }
        else actors.CreateActorProxy<IProjectionReplayPublicationActor>(Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(_ => throw new InvalidOperationException());
        var gateway = new DaprProjectionRebuildWriteGateway(actors);
        var request = new ProjectionReplayPublicationRequest(null!, new(ProjectionLifecyclePhase.Idle, 0), "operation", null);
        await using (var lease = new RouteCatalogLeaseReleaser(await fixture.Authorities[0].AcquireAsync()))
            await Should.ThrowAsync<Exception>(() => method switch
            {
                "StageReplayAsync" => gateway.StageReplayAsync("actor", request, default),
                "PromoteReplayAsync" => gateway.PromoteReplayAsync("actor", request, default),
                "FinalizeReplayAsync" => gateway.FinalizeReplayAsync("actor", request, default),
                _ => gateway.ReadReplayCompletionAsync("actor", request, default),
            });
        (await fixture.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(remote ? 1 : 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcreteLifecycleTransportFaultRetainsFenceEvenWhenQueryRouterHandlesItsError(bool failure)
    {
        var fixture = new RouteCatalogTestFixture(); var first = fixture.Candidate(); await fixture.ActivateAsync(first);
        int calls = 0;
        using var transport = new MockHttpMessageHandler((_, _) =>
        {
            calls++;
            if (failure) throw new HttpRequestException("lifecycle transport failure");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(JsonSerializer.Serialize(new ProjectionLifecycleSnapshot(ProjectionLifecyclePhase.Idle, 1), JsonSerializerOptions.Web)) });
        });
        var factory = new ActorProxyFactory(new ActorProxyOptions { HttpEndpoint = "http://localhost:3500", UseJsonSerialization = true, JsonSerializerOptions = JsonSerializerOptions.Web }, transport);
        var invoker = Substitute.For<IProjectionActorInvoker>();
        invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>()).Returns(new QueryResult(true, "{}"u8.ToArray(), null, "counter"));
        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance, new DaprProjectionLifecycleGateway(factory), catalogAuthority: fixture.Authorities[0]);
        (await router.RouteQueryAsync(new("tenant-a", "counter", "aggregate", "get-counter-status", [], "correlation", "user"))).Success.ShouldBeTrue();
        calls.ShouldBeGreaterThan(0); (await fixture.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(failure ? 1 : 0);
        var next = fixture.Candidate(2, first.RootDigest); await fixture.Authorities[0].PrepareAsync(next);
        if (failure) await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].ReadyAsync(next.AttemptId));
        else await fixture.Authorities[0].ReadyAsync(next.AttemptId);
    }

    [Fact]
    public async Task ActualHttpCatalogRefusalPreservesProblemMediaTypeAndPrePipelineShape()
    {
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddExceptionHandler<RouteCatalogUnavailableExceptionHandler>(); builder.Services.AddProblemDetails();
        await using var app = builder.Build(); app.UseExceptionHandler();
        app.MapGet("/refusal", () => Task.FromException<IResult>(new RouteCatalogUnavailableException("private target")));
        await app.StartAsync(); using var client = new HttpClient(); using var response = await client.GetAsync(app.Urls.Single() + "/refusal");
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable); response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        string body = await response.Content.ReadAsStringAsync(); body.ShouldNotContain("correlationId"); body.ShouldNotContain("private target");
        using var json = JsonDocument.Parse(body); json.RootElement.GetProperty("status").GetInt32().ShouldBe(503);
    }
}
