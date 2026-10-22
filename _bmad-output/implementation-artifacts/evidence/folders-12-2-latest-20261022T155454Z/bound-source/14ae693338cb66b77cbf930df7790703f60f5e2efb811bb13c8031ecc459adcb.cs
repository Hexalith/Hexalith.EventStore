using System.Net;
using System.Text.Json;

using Dapr;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Controllers;

public sealed class ProjectionSubscriptionMetadataTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("in-memory-events", false)]
    [InlineData("in-memory-events", true)]
    public async Task ProgrammaticSubscriptionUsesPublisherOptionsAndDeclarativeModeSuppressesIt(string? component, bool declarative)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["EventStore:ProjectionChanges:PubSubName"] = component ?? ProjectionChangeNotifierOptions.DefaultPubSubName });
        builder.Services.AddOptions<ProjectionChangeNotifierOptions>().Bind(builder.Configuration.GetSection("EventStore:ProjectionChanges"));
        builder.Services.AddControllers().AddApplicationPart(typeof(ProjectionNotificationController).Assembly).AddDapr();
        await using WebApplication app = builder.Build();
        app.MapControllers().Add(endpoint => ProjectionNotificationTopicAttribute.Bind(endpoint, app.Services, declarative));
        if (!declarative) app.MapSubscribeHandler();
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync("/dapr/subscribe", TestContext.Current.CancellationToken);
        RouteEndpoint receiver = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().Single(endpoint => endpoint.RoutePattern.RawText == "projections/changed");
        if (declarative)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            receiver.Metadata.GetMetadata<ITopicMetadata>().ShouldBeNull();
            return;
        }

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string publisherComponent = app.Services.GetRequiredService<IOptions<ProjectionChangeNotifierOptions>>().Value.PubSubName;
        using JsonDocument subscriptions = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        JsonElement subscription = subscriptions.RootElement.EnumerateArray().Single(subscription => subscription.GetProperty("topic").GetString() == "*.*.projection-changed");
        subscription.EnumerateObject().Single(property => property.Name.Equals("pubsubname", StringComparison.OrdinalIgnoreCase)).Value.GetString().ShouldBe(publisherComponent);
        publisherComponent.ShouldBe(component ?? "pubsub");
        receiver.Metadata.GetMetadata<ITopicMetadata>()!.PubsubName.ShouldBe(publisherComponent);
    }
}
