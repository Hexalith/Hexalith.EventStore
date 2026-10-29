using System.Net;
using System.Net.Http.Json;

using Dapr;
using Dapr.Client;

using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>
/// Proves framework HTTP admission before activation or persistence using owned Dapr/Redis infrastructure.
/// Direct app-port controls do not qualify actor-peer protocol safety or runtime invocation restrictions.
/// </summary>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public sealed class HttpHostSecurityLiveTests(DaprTestContainerFixture fixture)
{
    [Fact]
    public async Task RejectedFrameworkRequestsPreserveDurableStateAndNeverActivateActors()
    {
        fixture.ThrowIfHostStopped();
        string id = Guid.NewGuid().ToString("N");
        string actorKey = "story514-actor-" + id;
        string deliveryKey = "story514-delivery-" + id;
        using DaprClient dapr = new DaprClientBuilder().UseGrpcEndpoint(fixture.DaprGrpcEndpoint).Build();
        await dapr.SaveStateAsync("statestore", actorKey, "before", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        await dapr.SaveStateAsync("statestore", deliveryKey, "before", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        string actorBefore = await fixture.GetGenericStateJsonAsync(actorKey).ConfigureAwait(true);
        string deliveryBefore = await fixture.GetGenericStateJsonAsync(deliveryKey).ConfigureAwait(true);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration[DaprAppChannelToken.ConfigurationKey] = fixture.AppApiToken;
        builder.Services.AddSingleton(dapr);
        builder.Services.AddEventStoreHttpHostSecurity();
        builder.Services.AddActors(options => options.Actors.RegisterActor<HttpHostSecurityLiveActor>());
        await using WebApplication app = builder.Build();
        app.UseRouting();
        app.UseEventStoreHttpHostSecurity();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapActorsHandlers().RequireEventStoreChannel();
        app.MapSubscribeHandler().RequireEventStoreChannel();
        app.MapPost("/story514/delivery", async (string marker) =>
        {
            await dapr.SaveStateAsync("statestore", deliveryKey, marker).ConfigureAwait(false);
            return Results.Ok();
        }).WithTopic("pubsub", "story514.delivery").RequireEventStoreChannel();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using HttpClient client = new()
        {
            BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()),
        };
        string actorPath = "/actors/HttpHostSecurityLiveActor/" + id + "/method/RecordAsync";
        int activations = HttpHostSecurityLiveActor.Activations;
        foreach (string path in new[] { "/dapr/config", "/dapr/subscribe", "/healthz", "/story514/delivery", actorPath })
        {
            foreach (string credential in new[] { "missing", "wrong", "duplicate" })
            {
                bool post = path == actorPath || path == "/story514/delivery";
                using var request = new HttpRequestMessage(path == actorPath ? HttpMethod.Put : post ? HttpMethod.Post : HttpMethod.Get, path);
                if (post) request.Content = JsonContent.Create("denied");
                if (credential != "missing") request.Headers.TryAddWithoutValidation(DaprAppChannelToken.HeaderName,
                    credential == "duplicate" ? [fixture.AppApiToken, fixture.AppApiToken] : ["wrong"]);
                using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
                response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, path + " " + credential);
            }
        }
        HttpHostSecurityLiveActor.Activations.ShouldBe(activations);
        (await fixture.GetGenericStateJsonAsync(actorKey).ConfigureAwait(true)).ShouldBe(actorBefore);
        (await fixture.GetGenericStateJsonAsync(deliveryKey).ConfigureAwait(true)).ShouldBe(deliveryBefore);

        client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, fixture.AppApiToken);
        using HttpResponseMessage configuration = await client.GetAsync("/dapr/config", TestContext.Current.CancellationToken).ConfigureAwait(true);
        configuration.StatusCode.ShouldBe(HttpStatusCode.OK);
        using HttpResponseMessage subscription = await client.GetAsync("/dapr/subscribe", TestContext.Current.CancellationToken).ConfigureAwait(true);
        subscription.StatusCode.ShouldBe(HttpStatusCode.OK);
        using HttpResponseMessage delivered = await client.PostAsJsonAsync("/story514/delivery?marker=after", "after", TestContext.Current.CancellationToken).ConfigureAwait(true);
        delivered.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await fixture.GetGenericStateJsonAsync(deliveryKey).ConfigureAwait(true)).ShouldContain("after");
        using HttpResponseMessage activated = await client.PutAsJsonAsync(actorPath, "after", TestContext.Current.CancellationToken).ConfigureAwait(true);
        activated.StatusCode.ShouldBe(HttpStatusCode.OK);
        HttpHostSecurityLiveActor.Activations.ShouldBe(activations + 1);
        (await fixture.GetGenericStateJsonAsync(actorKey).ConfigureAwait(true)).ShouldContain("after");
        await dapr.DeleteStateAsync("statestore", actorKey, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        await dapr.DeleteStateAsync("statestore", deliveryKey, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
