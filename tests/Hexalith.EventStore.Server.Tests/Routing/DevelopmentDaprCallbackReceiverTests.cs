using Dapr;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Extensions;
using Hexalith.EventStore.Routing;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Actual SDK mapper/action observation, including configured component and declarative absence.</summary>
public sealed class DevelopmentDaprCallbackReceiverTests
{
    [Theory]
    [InlineData("pubsub", false)]
    [InlineData("in-memory-events", false)]
    [InlineData("in-memory-events", true)]
    public async Task ActualMapperAndControllerBindConfiguredTopicAndRefuseForeignCodeAndPolicy(string component, bool declarative)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EventStore:ProjectionChanges:PubSubName"] = component,
            ["Dapr:DeclarativeSubscriptions"] = declarative.ToString(),
        });
        builder.Services.AddOptions<ProjectionChangeNotifierOptions>().BindConfiguration("EventStore:ProjectionChanges");
        builder.Services.AddControllers().AddApplicationPart(typeof(ProjectionNotificationController).Assembly).AddDapr();
        await using var app = builder.Build();
        app.MapControllers().WithEventStoreProjectionNotifications(app.Services, declarative);
        if (!declarative) app.MapSubscribeHandler().RequireEventStoreChannel();
        await app.StartAsync(TestContext.Current.CancellationToken);
        var source = app.Services.GetRequiredService<EndpointDataSource>();
        var observer = new DevelopmentDaprCallbackReceiver(source, app.Configuration, app.Services.GetRequiredService<IOptions<ProjectionChangeNotifierOptions>>());
        var notification = source.Endpoints.OfType<RouteEndpoint>().Single(endpoint => endpoint.RoutePattern.RawText?.Trim('/') == "projections/changed");
        var captured = observer.Observe(notification).ShouldNotBeNull();
        captured.Kind.ShouldBe("projection-notification"); captured.Verbs.ShouldBe("POST");
        captured.CodeDigest.Length.ShouldBe(64); captured.FrameworkDigest.Length.ShouldBe(64); captured.InventoryDigest.Length.ShouldBe(64);
        var foreign = new RouteEndpoint(_ => Task.CompletedTask, notification.RoutePattern, notification.Order, notification.Metadata, notification.DisplayName);
        observer.Observe(foreign).ShouldBeNull();
        var anonymous = new RouteEndpoint(notification.RequestDelegate, notification.RoutePattern, notification.Order,
            new EndpointMetadataCollection(notification.Metadata.Concat([new AllowAnonymousAttribute()])), notification.DisplayName);
        observer.Observe(anonymous).ShouldBeNull();
        var context = new DefaultHttpContext(); context.Request.Method = "GET"; context.Request.Path = "/dapr/subscribe";
        observer.IsAbsentDiscovery(context).ShouldBe(declarative);
        context.SetEndpoint(notification); observer.IsAbsentDiscovery(context).ShouldBeFalse();
        if (!declarative)
        {
            var discovery = source.Endpoints.OfType<RouteEndpoint>().Single(endpoint => endpoint.RoutePattern.RawText?.Trim('/') == "dapr/subscribe");
            observer.Observe(discovery).ShouldNotBeNull().Kind.ShouldBe("subscribe");
            observer.Observe(new RouteEndpoint(_ => Task.CompletedTask, discovery.RoutePattern, discovery.Order, discovery.Metadata, discovery.DisplayName)).ShouldBeNull();
            var wrongTopic = new RouteEndpoint(notification.RequestDelegate, notification.RoutePattern, notification.Order,
                new EndpointMetadataCollection(notification.Metadata.Where(item => item is not ITopicMetadata).Concat([new TopicAttribute("wrong-component", "*.*.projection-changed")])), notification.DisplayName);
            observer.Observe(wrongTopic).ShouldBeNull();
            foreach (var changedTopic in new[]
            {
                new TopicAttribute(component, "*.*.projection-changed") { EnableRawPayload = true },
                new TopicAttribute(component, "*.*.projection-changed") { DeadLetterTopic = "unowned-dead-letter" },
                new TopicAttribute(component, "*.*.projection-changed") { MetadataSeparator = ";" },
            })
            {
                var mutated = new RouteEndpoint(notification.RequestDelegate, notification.RoutePattern, notification.Order,
                    new EndpointMetadataCollection(notification.Metadata.Where(item => item is not ITopicMetadata).Concat([changedTopic])), notification.DisplayName);
                observer.Observe(mutated).ShouldBeNull();
            }
        }
        var changed = new DevelopmentDaprCallbackReceiver(source, app.Configuration, Options.Create(new ProjectionChangeNotifierOptions { PubSubName = "changed-after-capture" }));
        if (!declarative) changed.Observe(notification).ShouldBeNull();
        else changed.Observe(notification).ShouldNotBeNull().InventoryDigest.ShouldNotBe(captured.InventoryDigest);
    }
}
