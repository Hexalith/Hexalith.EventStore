using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

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
using StackExchange.Redis;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>
/// Proves framework HTTP admission before activation or persistence using owned Dapr/Redis infrastructure.
/// Direct app-port controls do not qualify actor-peer protocol safety or runtime invocation restrictions.
/// </summary>
/// <param name="output">Receives nonsecret infrastructure identities and cleanup observations.</param>
[Trait("Category", "LiveSidecar")]
public sealed class HttpHostSecurityLiveTests(ITestOutputHelper output)
{
    /// <summary>Uses a private Redis and state-only sidecar to prove HTTP admission before durable effects.</summary>
    [Fact]
    public async Task RejectedFrameworkRequestsPreserveDurableStateAndNeverActivateActors()
    {
        string id = Guid.NewGuid().ToString("N");
        string channel = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        string scratch = Path.Combine(Path.GetTempPath(), "eventstore-http-security-" + id);
        if (Directory.Exists(scratch)) throw new InvalidOperationException("Refusing to reuse an existing fixture directory.");
        Directory.CreateDirectory(scratch);
        string ownership = Path.Combine(scratch, "owner");
        await File.WriteAllTextAsync(ownership, id, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using var redis = new RouteCatalogDevelopmentRedis();
        RouteCatalogDevelopmentSidecar? sidecar = null;
        try
        {
            await redis.StartAsync().ConfigureAwait(true);
            string container = redis.ContainerId;
            output.WriteLine(JsonSerializer.Serialize(new { stage = "redis-owned", container, redis.Endpoint, dockerEndpoint = redis.Docker.Endpoint, image = RouteCatalogDevelopmentRedis.Image }));
            string resources = Path.Combine(scratch, "components");
            Directory.CreateDirectory(resources);
            string component = Path.Combine(resources, "state.yaml");
            string configurationPath = Path.Combine(scratch, "configuration.yaml");
            await File.WriteAllBytesAsync(component, JsonSerializer.SerializeToUtf8Bytes(new
            {
                apiVersion = "dapr.io/v1alpha1", kind = "Component", metadata = new { name = "statestore" },
                spec = new { type = "state.redis", version = "v1", metadata = new[]
                { new { name = "redisHost", value = redis.Endpoint }, new { name = "redisPassword", value = "" }, new { name = "keyPrefix", value = "none" } } },
                scopes = new[] { "http-security-" + id },
            }), TestContext.Current.CancellationToken).ConfigureAwait(true);
            await File.WriteAllBytesAsync(configurationPath, JsonSerializer.SerializeToUtf8Bytes(new
            {
                apiVersion = "dapr.io/v1alpha1", kind = "Configuration", metadata = new { name = "http-security-development-fixture" },
                spec = new { accessControl = new { defaultAction = "deny", trustDomain = "public" } },
            }), TestContext.Current.CancellationToken).ConfigureAwait(true);
            sidecar = new RouteCatalogDevelopmentSidecar("http-security-" + id, resources, configurationPath);
            sidecar.DisableControlPlane();
            await sidecar.StartAsync().ConfigureAwait(true);
            output.WriteLine(JsonSerializer.Serialize(new
            {
                stage = "sidecar-owned", sidecar.AppId, sidecar.ProcessId, sidecar.Executable, sidecar.HttpEndpoint,
                sidecar.GrpcEndpoint, sidecar.RuntimeFingerprint, arguments = JsonSerializer.Deserialize<string[]>(sidecar.Arguments),
                placement = "disabled", scheduler = "disabled", importedAppApiToken = false, importedDaprApiToken = false,
            }));
            using ConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(redis.Endpoint).ConfigureAwait(true);
            IDatabase database = connection.GetDatabase();
            async Task<string> Persisted(string key)
                => (await database.HashGetAsync(key, "data").ConfigureAwait(false)).ToString();
            string actorKey = "story514-actor-" + id;
            string deliveryKey = "story514-delivery-" + id;
            using DaprClient dapr = new DaprClientBuilder().UseGrpcEndpoint(sidecar.GrpcEndpoint).Build();
            await dapr.SaveStateAsync("statestore", actorKey, "before", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
            await dapr.SaveStateAsync("statestore", deliveryKey, "before", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
            string actorBefore = await Persisted(actorKey).ConfigureAwait(true);
            string deliveryBefore = await Persisted(deliveryKey).ConfigureAwait(true);
            actorBefore.ShouldContain("before");
            deliveryBefore.ShouldContain("before");

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration[DaprAppChannelToken.ConfigurationKey] = channel;
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
                BaseAddress = new Uri(app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()),
            };
            output.WriteLine(JsonSerializer.Serialize(new { stage = "application-owned", endpoint = client.BaseAddress!.AbsoluteUri }));
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
                        credential == "duplicate" ? [channel, channel] : ["wrong"]);
                    using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
                    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, path + " " + credential);
                }
            }
            HttpHostSecurityLiveActor.Activations.ShouldBe(activations);
            (await Persisted(actorKey).ConfigureAwait(true)).ShouldBe(actorBefore);
            (await Persisted(deliveryKey).ConfigureAwait(true)).ShouldBe(deliveryBefore);

            client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, channel);
            using HttpResponseMessage configuration = await client.GetAsync("/dapr/config", TestContext.Current.CancellationToken).ConfigureAwait(true);
            configuration.StatusCode.ShouldBe(HttpStatusCode.OK);
            using HttpResponseMessage subscription = await client.GetAsync("/dapr/subscribe", TestContext.Current.CancellationToken).ConfigureAwait(true);
            subscription.StatusCode.ShouldBe(HttpStatusCode.OK);
            using HttpResponseMessage delivered = await client.PostAsJsonAsync("/story514/delivery?marker=after", "after", TestContext.Current.CancellationToken).ConfigureAwait(true);
            delivered.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await Persisted(deliveryKey).ConfigureAwait(true)).ShouldContain("after");
            using HttpResponseMessage activated = await client.PutAsJsonAsync(actorPath, "after", TestContext.Current.CancellationToken).ConfigureAwait(true);
            activated.StatusCode.ShouldBe(HttpStatusCode.OK);
            HttpHostSecurityLiveActor.Activations.ShouldBe(activations + 1);
            (await Persisted(actorKey).ConfigureAwait(true)).ShouldContain("after");
            await dapr.DeleteStateAsync("statestore", actorKey, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
            await dapr.DeleteStateAsync("statestore", deliveryKey, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
        finally
        {
            if (sidecar is not null)
            {
                await sidecar.DisposeAsync().ConfigureAwait(true);
                output.WriteLine("sidecar-cleanup: retained owned process stopped");
            }
            await redis.DisposeAsync().ConfigureAwait(true);
            output.WriteLine("redis-cleanup: exact container ID and owner label checked before removal");
            if (!File.Exists(ownership) || await File.ReadAllTextAsync(ownership, TestContext.Current.CancellationToken).ConfigureAwait(true) != id)
                throw new InvalidOperationException("Refusing cleanup without the exact fixture-directory owner marker.");
            Directory.Delete(scratch, recursive: true);
            output.WriteLine("scratch-cleanup: exact owner marker checked before removal");
        }
    }
}
