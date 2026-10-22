using Dapr.Actors.Runtime;
using Dapr.Client;

using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed class SharedProjectionSourceActorRuntimeRegistrationTests
{
    [Fact]
    public async Task ProductionRegistrationResolvesRebuildOperatorAndRegistersRosterActor()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(Substitute.For<DaprClient>());
        _ = builder.Services.AddEventStoreServer(builder.Configuration);
        await using WebApplication app = builder.Build();
        app.MapActorsHandlers();
        await app.StartAsync(TestContext.Current.CancellationToken);

        using IServiceScope scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ISharedProjectionSourceRebuildOperator>()
            .ShouldBeOfType<SharedProjectionSourceBeginProducer>();
        app.Services.GetRequiredService<ActorRuntime>().RegisteredActors.ShouldContain(registration =>
            registration.Type.ImplementationType == typeof(SharedProjectionSourceRosterActor)
            && registration.Type.ActorTypeName == SharedProjectionSourceRosterActor.ActorTypeName);
    }

    [Fact]
    public async Task RosterActorRegistersWithDaprRuntimeAndPublishesItsConfiguration()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddActors(options => options.Actors.RegisterActor<SharedProjectionSourceRosterActor>(
            SharedProjectionSourceRosterActor.ActorTypeName));
        await using WebApplication app = builder.Build();
        app.MapActorsHandlers();
        await app.StartAsync(TestContext.Current.CancellationToken);

        ActorRuntime runtime = app.Services.GetRequiredService<ActorRuntime>();
        runtime.RegisteredActors.ShouldContain(registration =>
            registration.Type.ImplementationType == typeof(SharedProjectionSourceRosterActor)
            && registration.Type.ActorTypeName == SharedProjectionSourceRosterActor.ActorTypeName);

        using HttpResponseMessage response = await app.GetTestClient().GetAsync(
            "/dapr/config", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain(SharedProjectionSourceRosterActor.ActorTypeName);
    }

    [Fact]
    public async Task RuntimeRegistrationRejectsAnOptionalActorInterfaceParameter()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddActors(options => options.Actors.RegisterActor<InvalidOptionalActor>());
        await using WebApplication app = builder.Build();

        Exception? failure = null;
        try
        {
            app.MapActorsHandlers();
            await app.StartAsync(TestContext.Current.CancellationToken);
            using HttpResponseMessage response = await app.GetTestClient().GetAsync(
                "/dapr/config", TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        failure.ShouldNotBeNull().ToString().ShouldContain("optional parameter");
    }

    public interface IInvalidOptionalActor : Dapr.Actors.IActor
    {
        Task<bool> ProbeAsync(bool optional = false);
    }

    public sealed class InvalidOptionalActor(ActorHost host) : Actor(host), IInvalidOptionalActor
    {
        public Task<bool> ProbeAsync(bool optional = false) => Task.FromResult(optional);
    }
}
