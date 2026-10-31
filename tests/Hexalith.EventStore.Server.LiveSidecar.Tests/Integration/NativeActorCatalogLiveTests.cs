using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;
using Dapr.Client;
using Hexalith.EventStore;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Extensions;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Server.Routing;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Real pinned daprd and native SDK actor callbacks under the private technical Gateway fence.</summary>
public sealed class NativeActorCatalogLiveTests
{
    [Fact]
    public async Task NativeQueryRegisteredActorsAndLifecycleKeepActualChannelCatalogAndMethodBoundaries()
    {
        string directory = Path.Combine(Path.GetTempPath(), "native-catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string appId = "native-catalog-" + Guid.NewGuid().ToString("N");
        string appToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await using var redis = new RouteCatalogDevelopmentRedis();
        await redis.StartAsync();
        string resources = Path.Combine(directory, "components"); Directory.CreateDirectory(resources);
        async Task Component(string name, string type, bool actor)
        {
            var metadata = new List<object> { new { name = "redisHost", value = redis.Endpoint }, new { name = "redisPassword", value = "" } };
            if (actor) metadata.Add(new { name = "actorStateStore", value = "true" });
            await File.WriteAllTextAsync(Path.Combine(resources, name + ".yaml"), JsonSerializer.Serialize(new { apiVersion = "dapr.io/v1alpha1", kind = "Component", metadata = new { name, @namespace = appId }, spec = new { type, version = "v1", metadata }, scopes = new[] { appId } }));
        }
        await Component("statestore", "state.redis", true); await Component("pubsub", "pubsub.redis", false);
        string aclPath = Path.Combine(directory, "acl.json");
        await File.WriteAllTextAsync(aclPath, JsonSerializer.Serialize(new { apiVersion = "dapr.io/v1alpha1", kind = "Configuration", metadata = new { name = appId }, spec = new { accessControl = new { defaultAction = "deny", trustDomain = "public", policies = new[] { new { appId, @namespace = appId, trustDomain = "public", defaultAction = "deny", operations = new[] { new { name = "/**", httpVerb = new[] { "GET", "POST", "PUT" }, action = "allow" } } } } } } }));
        string componentPath = Path.Combine(directory, "authority.json");
        await File.WriteAllTextAsync(componentPath, JsonSerializer.Serialize(new { kind = "Component", metadata = new { name = "catalog-test", @namespace = appId }, spec = new { type = "state.redis", metadata = new[] { new { name = "keyPrefix", value = "none" } } }, scopes = new[] { appId } }));
        {
            var authority = new { apiVersion = "dapr.io/v1alpha1", kind = "Component", metadata = new { name = "catalog-test", @namespace = appId }, spec = new { type = "state.redis", version = "v1", metadata = new[] { new { name = "keyPrefix", value = "none" }, new { name = "redisHost", value = redis.Endpoint }, new { name = "redisPassword", value = "" } } }, scopes = new[] { appId } };
            await File.WriteAllTextAsync(componentPath, JsonSerializer.Serialize(authority));
            await File.WriteAllTextAsync(Path.Combine(resources, "catalog-test.yaml"), JsonSerializer.Serialize(authority));
        }
        await using var sidecar = new RouteCatalogDevelopmentSidecar(appId, resources, aclPath, appToken);
        sidecar.ConfigureNativeActorNamespace(appId);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", Args = [] });
        builder.WebHost.UseUrls($"http://127.0.0.1:{sidecar.AppPort}");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["APP_API_TOKEN"] = appToken, ["NAMESPACE"] = appId, ["DAPR_HTTP_ENDPOINT"] = sidecar.HttpEndpoint, ["DAPR_GRPC_ENDPOINT"] = sidecar.GrpcEndpoint,
            ["EventStore:DomainService:AppId"] = appId, ["EventStore:DomainService:ServiceVersion"] = "v1",
            ["EventStore:DataProtection:PersistToStateStore"] = "false",
            ["Authentication:JwtBearer:Issuer"] = "native-catalog", ["Authentication:JwtBearer:Audience"] = appId,
            ["Authentication:JwtBearer:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)), ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
            ["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"] = directory, ["EventStore:RouteCatalog:DevelopmentBootstrap:HostId"] = "native",
            ["EventStore:RouteCatalog:DevelopmentBootstrap:RequiredHostIds:0"] = "native", ["EventStore:RouteCatalog:DevelopmentBootstrap:SelectedAppIds:0"] = appId,
            ["EventStore:RouteCatalog:DevelopmentBootstrap:ComponentPath"] = componentPath, ["EventStore:RouteCatalog:DevelopmentBootstrap:AccessControlPath"] = aclPath,
            ["EventStore:RouteCatalog:DevelopmentBootstrap:DeclaredInvocations"] = JsonSerializer.Serialize(new[] { new RouteCatalogDeclaredInvocation(appId, "/**", ["GET", "POST", "PUT"]) }),
        });
        builder.Services.AddEventStore(); builder.Services.AddEventStoreServer(builder.Configuration); builder.Services.AddEventStoreDomainQueryRouting();
        builder.Services.AddControllers().AddApplicationPart(typeof(Hexalith.EventStore.Controllers.CommandsController).Assembly);
        // In-process hosting has no Aspire-injected process environment. Configure the real
        // native client after the API registration, using this owned sidecar's actual endpoints.
        builder.Services.RemoveAll<DaprClient>();
        builder.Services.AddSingleton(new DaprClientBuilder().UseHttpEndpoint(sidecar.HttpEndpoint).UseGrpcEndpoint(sidecar.GrpcEndpoint).Build());
        builder.Services.Configure<ActorRuntimeOptions>(options => options.HttpEndpoint = sidecar.HttpEndpoint);
        var proxyFactory = new ActorProxyFactory(new ActorProxyOptions { HttpEndpoint = sidecar.HttpEndpoint });
        builder.Services.AddSingleton<IActorProxyFactory>(proxyFactory);
        builder.Services.AddSingleton<FiniteRouteCatalogReceiver>();
        builder.Services.AddSingleton<IDevelopmentNativeActorReceiver, DevelopmentNativeActorReceiver>();

        builder.Services.Configure<RouteCatalogHostOptions>(options => { options.DevelopmentFixture = true; options.Profile = "Development"; options.StateStore = "catalog-test"; options.StateKey = "native-" + appId; });
        builder.Services.AddEventStoreRouteCatalogObservation(async (provider, token) =>
        {
            using var scope = provider.CreateScope();
            var surface = provider.GetRequiredService<FiniteRouteCatalogReceiver>().Observe();
            var declaration = CompiledRouteCatalogDeclarations.Read([typeof(SampleCatalog).Assembly]).Single(declaration => RouteCatalogProjectionActorPolicy.IsQuery(declaration));
            var target = RunScopedRouteCatalogGateway.ActorQueryTargets(scope.ServiceProvider, [typeof(SampleCatalog).Assembly], appId).Single();
            return (await DevelopmentRouteCatalogInputs.CaptureAsync(builder.Configuration, appId, [declaration], [target], [], [], DevelopmentTechnicalHttpTransport.Auxiliary(surface, appId, "gateway"), token))
                with { Operations = SampleCatalog.Operations.Where(operation => operation.RouteId == declaration.RouteId).ToArray(), ReceiverSurface = surface };
        });
        await using var app = builder.Build();
        var store = app.Services.GetRequiredService<IRouteCatalogStateStore>();
        app.UseRouting();
        var reminderObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Use(async (context, next) =>
        {
            await next(context);
            if (context.Request.Path.Value?.EndsWith("/method/remind/native-catalog-proof", StringComparison.Ordinal) == true
                && context.Response.StatusCode == StatusCodes.Status200OK) reminderObserved.TrySetResult(true);
        });
        app.UseEventStoreHttpHostSecurity(); app.UseAuthentication(); app.UseAuthorization();
        app.MapControllers(); app.MapActorsHandlers().WithMetadata(EventStoreActorCallbackEndpointMetadata.Instance).RequireEventStoreChannel();
        FiniteRouteCatalogReceiver.UseFiniteDevelopmentRouteCatalogReceiver(app, RunScopedRouteCatalogGateway.Operation, technical: true);
        try
        {
            await app.StartAsync(TestContext.Current.CancellationToken); await sidecar.StartAsync();
            // The native API's configuration-sync hosted service can create this real gRPC
            // channel before daprd starts. Observe gRPC readiness after outbound HTTP readiness;
            // only transient read-only connection startup is retried, never actor/catalog work.
            var client = app.Services.GetRequiredService<DaprClient>();
            using var grpcReady = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            grpcReady.CancelAfter(TimeSpan.FromSeconds(30));
            while (true)
            {
                sidecar.RequireRunning();
                try
                {
                    var loaded = await client.GetMetadataAsync(grpcReady.Token);
                    loaded.Components.Select(component => component.Name).ShouldContain("catalog-test");
                    loaded.Components.Select(component => component.Name).ShouldContain("statestore");
                    break;
                }
                catch (Dapr.DaprException exception) when (exception.InnerException is Grpc.Core.RpcException { StatusCode: Grpc.Core.StatusCode.Unavailable })
                { await Task.Delay(100, grpcReady.Token); }
            }
            using var scope = app.Services.CreateScope();
            var observer = app.Services.GetRequiredService<FiniteRouteCatalogReceiver>().Observe();
            observer.Endpoints.Count(endpoint => endpoint.NativeActor != null).ShouldBe(6);
            var observation = await DevelopmentRouteCatalogInputs.CaptureAsync(builder.Configuration, appId,
                CompiledRouteCatalogDeclarations.Read([typeof(SampleCatalog).Assembly]).Where(RouteCatalogProjectionActorPolicy.IsQuery).ToArray(), RunScopedRouteCatalogGateway.ActorQueryTargets(scope.ServiceProvider, [typeof(SampleCatalog).Assembly], appId),
                [], [], DevelopmentTechnicalHttpTransport.Auxiliary(observer, appId, "gateway"));
            observation = observation with { ReceiverSurface = observer, Operations = SampleCatalog.Operations.Where(operation => observation.Declarations.Any(declaration => declaration.RouteId == operation.RouteId)).ToArray(), AuthorityCoordinates = store.Coordinates };
            var identity = ObservedRouteCatalogHost.Identity(observation); app.Services.GetRequiredService<IOptions<RouteCatalogHostOptions>>().Value.Identity = identity;
            var envelope = RouteCatalogCodec.Seal(new RouteCatalogEnvelope(3, 1, "Development", false, observation.Declarations,
                observation.Targets.Select(target => target with { OperationContractDigest = observation.Operations.Single(operation => operation.RouteId == target.RouteId).Digest }).ToArray(), [], "", "", observation.Operations, []));
            var activation = new RouteCatalogActivation("native-actors", RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), null, [identity], "Development-fixture", "", observation.AuxiliaryOperations);
            activation = activation with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(activation) };
            RouteCatalogTopologyValidator.ValidateDevelopment(observation.TopologyDocuments[0], observation.TopologyDocuments[1], envelope, [identity], appId,
                observation.AuxiliaryOperations, observation.ReceiverSurface, observation.SelectedAppIds, observation.DeclaredInvocations);
            var authority = app.Services.GetRequiredService<IRouteCatalogAuthority>();
            await authority.PrepareAsync(activation); await authority.ReadyAsync(activation.AttemptId); await authority.CommitAsync(activation.AttemptId);
            string actorId = "counter:tenant-a:one";
            var actor = proxyFactory.Create(new ActorId(actorId), "ProjectionActor");
            await actor.InvokeMethodAsync(nameof(IProjectionWriteActor.UpdateProjectionAsync), ProjectionState.FromJsonElement("counter", "tenant-a", JsonSerializer.SerializeToElement(new { count = 7 })), TestContext.Current.CancellationToken);
            using var direct = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{sidecar.AppPort}") };
            // Re-activation must load the previously persisted projection from the real actor store.
            using (var deactivatePersisted = new HttpRequestMessage(HttpMethod.Delete, $"/actors/ProjectionActor/{actorId}"))
            {
                deactivatePersisted.Headers.Add("dapr-api-token", appToken);
                using var deactivatedPersisted = await direct.SendAsync(deactivatePersisted, TestContext.Current.CancellationToken);
                deactivatedPersisted.StatusCode.ShouldBe(HttpStatusCode.OK);
            }
            var result = await new DefaultProjectionActorInvoker(proxyFactory).InvokeAsync(actorId, "ProjectionActor", new QueryEnvelope("tenant-a", "counter", "one", "get-counter-status", [], "native-query", "native-user", "one"), TestContext.Current.CancellationToken);
            result.Success.ShouldBeTrue(); result.GetPayload().GetProperty("count").GetInt32().ShouldBe(7);
            var aggregate = proxyFactory.Create(new ActorId("tenant-a:counter:one"), "AggregateActor");
            await aggregate.InvokeMethodAsync<AggregateStreamMetadata>(nameof(IAggregateActor.GetStreamMetadataAsync), TestContext.Current.CancellationToken);
            foreach (string kind in new[] { "wrong-channel", "remote-service", "unknown-actor", "unknown-method" })
            {
                string path = kind == "unknown-actor" ? "/actors/UnknownActor/one/method/QueryAsync" : kind == "unknown-method" ? "/actors/ProjectionActor/one/method/Unknown" : "/actors/ProjectionActor/one/method/QueryAsync";
                using var request = new HttpRequestMessage(HttpMethod.Put, path); request.Headers.Add("dapr-api-token", kind == "wrong-channel" ? "forged" : appToken);
                if (kind == "remote-service") request.Headers.Add("dapr-caller-app-id", "foreign-caller");
                using var denied = await direct.SendAsync(request, TestContext.Current.CancellationToken);
                denied.StatusCode.ShouldBe(kind == "wrong-channel" ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden);
            }
            // Schedule the actual Dapr reminder transport and observe the real SDK IRemindable callback.
            using (var scheduler = new HttpClient { BaseAddress = new Uri(sidecar.HttpEndpoint) })
            {
                string reminderPath = "/v1.0/actors/AggregateActor/tenant-a:counter:one/reminders/native-catalog-proof";
                using var registered = await scheduler.PutAsJsonAsync(reminderPath,
                    new { dueTime = "0s", period = "1s", data = Convert.ToBase64String("{}"u8) }, TestContext.Current.CancellationToken);
                registered.IsSuccessStatusCode.ShouldBeTrue();
                try { await reminderObserved.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken); }
                finally
                {
                    using var removed = await scheduler.DeleteAsync(reminderPath, TestContext.Current.CancellationToken);
                    removed.IsSuccessStatusCode.ShouldBeTrue();
                }
            }
            using var deactivate = new HttpRequestMessage(HttpMethod.Delete, $"/actors/ProjectionActor/{actorId}"); deactivate.Headers.Add("dapr-api-token", appToken);
            using var deactivated = await direct.SendAsync(deactivate, TestContext.Current.CancellationToken); deactivated.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
        }
        finally { await sidecar.StopAsync(); await app.StopAsync(CancellationToken.None); Directory.Delete(directory, recursive: true); }
    }
}
