using Dapr.Actors.Runtime;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class DevelopmentNativeActorReceiverTests
{
    [Fact]
    public async Task ActualNativeDelegatesBindAllRegisteredSdkMethodsAndLifecycleKinds()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["EventStore:DomainService:AppId"] = "native-owned"; builder.Configuration["NAMESPACE"] = "native-private";
        builder.Services.AddEventStoreServer(builder.Configuration);
        builder.Services.AddEventStoreHttpHostSecurity();
        await using var app = builder.Build();
        app.MapActorsHandlers().RequireEventStoreChannel();
        var native = new DevelopmentNativeActorReceiver(app.Services.GetRequiredService<ActorRuntime>(), app.Services.GetRequiredService<IOptions<ActorRuntimeOptions>>(), app.Services.GetRequiredService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>(), builder.Configuration);
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().ToArray();
        endpoints.Length.ShouldBe(6);
        foreach (var endpoint in endpoints)
        {
            var observed = native.Observe(endpoint);
            observed.ShouldNotBeNull(endpoint.RoutePattern.RawText + " => " + endpoint.RequestDelegate!.Method + " target " + endpoint.RequestDelegate.Target?.GetType());
            observed.CodeDigest.Length.ShouldBe(64); observed.FrameworkDigest.Length.ShouldBe(64); observed.InventoryDigest.Length.ShouldBe(64);
        }
        var callback = endpoints.Single(endpoint => endpoint.RoutePattern.RawText == "actors/{actorTypeName}/{actorId}/method/{methodName}");
        var context = new DefaultHttpContext(); context.SetEndpoint(callback);
        context.Request.RouteValues["actorTypeName"] = "ProjectionActor"; context.Request.RouteValues["actorId"] = "counter:tenant-a:one";
        context.Request.RouteValues["methodName"] = "QueryAsync";
        native.Allows(context).ShouldBeTrue();
        context.Request.RouteValues["methodName"] = "UnknownMethod"; native.Allows(context).ShouldBeFalse();
        context.Request.RouteValues["methodName"] = "QueryAsync"; context.Request.RouteValues["actorTypeName"] = "UnknownActor"; native.Allows(context).ShouldBeFalse();
        context.Request.RouteValues["actorTypeName"] = "ProjectionActor";
        context.Request.Headers["dapr-caller-app-id"] = "native-owned"; context.Request.Headers["dapr-caller-namespace"] = "native-private";
        native.Allows(context).ShouldBeTrue();
        context.Request.Headers["dapr-caller-app-id"] = new Microsoft.Extensions.Primitives.StringValues(["native-owned", "foreign"]); native.Allows(context).ShouldBeFalse();
        context.Request.Headers["dapr-caller-app-id"] = "native-owned"; context.Request.Headers["dapr-caller-namespace"] = "foreign-namespace"; native.Allows(context).ShouldBeFalse();
        context.Request.Headers["dapr-caller-namespace"] = ""; native.Allows(context).ShouldBeFalse();
        context.Request.Headers["dapr-caller-namespace"] = new Microsoft.Extensions.Primitives.StringValues(["native-private", "native-private"]); native.Allows(context).ShouldBeFalse();
        context.Request.Headers["dapr-caller-namespace"] = "native-private"; context.Request.Headers["dapr-callee-app-id"] = "native-owned"; native.Allows(context).ShouldBeTrue();
        context.Request.Headers["dapr-callee-app-id"] = "foreign"; native.Allows(context).ShouldBeFalse();
        context.Request.Headers.Remove("dapr-callee-app-id"); context.Request.Headers["dapr-caller-app-id"] = "other-app"; native.Allows(context).ShouldBeFalse();
        context.Request.Headers.Clear(); context.Request.RouteValues["actorTypeName"] = "AggregateActor"; context.Request.RouteValues["methodName"] = "ProcessAuthorizedCommandAsync";
        native.Allows(context).ShouldBeTrue();
        var foreign = new RouteEndpoint(_ => Task.CompletedTask, callback.RoutePattern, callback.Order, callback.Metadata, callback.DisplayName);
        native.Observe(foreign).ShouldBeNull();
        var actors = app.Services.GetRequiredService<IOptions<ActorRuntimeOptions>>().Value.Actors;
        actors.Remove(actors.Single(actor => actor.Type.ActorTypeName == "ProjectionActor").Type);
        context.Request.RouteValues["actorTypeName"] = "ProjectionActor"; context.Request.RouteValues["methodName"] = "QueryAsync";
        native.Allows(context).ShouldBeFalse();
    }
}
