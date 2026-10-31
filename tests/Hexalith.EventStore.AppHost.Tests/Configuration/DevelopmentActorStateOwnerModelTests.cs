using System.Security.Cryptography;
using System.Text.Json;

using global::Aspire.Hosting;
using global::Aspire.Hosting.ApplicationModel;
using global::Aspire.Hosting.Eventing;
using global::Aspire.Hosting.Lifecycle;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.Aspire;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.AppHost.Tests.Configuration;

/// <summary>Uses the ordinary toolkit renderer without executing DCP or Dapr.</summary>
public sealed class DevelopmentActorStateOwnerModelTests
{
    [Theory(Timeout = 30000)]
    [InlineData("generated")]
    [InlineData("existing")]
    [InlineData("late-port")]
    [InlineData("late-owner-token")]
    [InlineData("late-receiver-token")]
    [InlineData("late-receiver-endpoint")]
    [InlineData("missing-port")]
    [InlineData("default-app-id")]
    [InlineData("late-argv-id")]
    [InlineData("late-argv-port")]
    [InlineData("late-allocation")]
    [InlineData("receipt-link")]
    [InlineData("existing-receipt")]
    [InlineData("localhost-allocation")]
    [InlineData("ipv6-allocation")]
    [InlineData("wrong-loopback-address")]
    [InlineData("actual-sdk-default")]
    public async Task RenderedOwnerCoordinatesAndPrivateCredentialHandoffCannotDiverge(string scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        string input = Directory.CreateTempSubdirectory("catalog-owner-model-").FullName;
        try
        {
            string config = Path.Combine(input, "configuration.json"); string state = Path.Combine(input, "state.json");
            await File.WriteAllTextAsync(config, """{"kind":"Configuration","spec":{"accessControl":{"defaultAction":"deny","trustDomain":"public"}}}""", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(state, """{"kind":"Component","spec":{"type":"state.redis","metadata":[{"name":"redisHost","value":"localhost:16379"}]}}""", TestContext.Current.CancellationToken);
            var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
            builder.Configuration["DcpPublisher:CliPath"] = Path.Combine(input, "model-only-dcp-not-started");
            builder.Configuration["DcpPublisher:DashboardPath"] = Path.Combine(input, "model-only-dashboard-not-started");
            builder.Environment.EnvironmentName = "Development";
            string command = Path.Combine(input, "model-only-dapr-not-started"); builder.AddDapr(options => options.DaprPath = command);
            IResourceBuilder<ProjectResource> Project(string name, string id, int? port = null) => builder.AddProject<EventStoreProjectMetadata>(name)
                .WithDaprSidecar(sidecar => sidecar.WithOptions(new DaprSidecarOptions { AppId = id, Config = config, DaprHttpPort = port }));
            bool sdkDefault = scenario == "actual-sdk-default";
            string ownerAppId = sdkDefault ? "eventstore" : "gateway-owned";
            var gateway = sdkDefault ? builder.AddProject<EventStoreProjectMetadata>("gateway") : Project("gateway", ownerAppId, scenario == "missing-port" ? null : 48123);
            var domain = Project("domain", "domain-owned"); var admin = sdkDefault ? builder.AddProject<EventStoreProjectMetadata>("admin") : Project("admin", "admin-owned");
            if (sdkDefault) builder.AddHexalithEventStore(gateway, admin, eventStoreDaprConfigPath: config, adminServerDaprConfigPath: config, eventStoreDaprHttpPort: 48123);
            var sidecar = gateway.Resource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
            string existing = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            string replacement = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            if (scenario == "existing") sidecar.Annotations.Add(new EnvironmentCallbackAnnotation(context => context.EnvironmentVariables["DAPR_API_TOKEN"] = existing));
            if (!sdkDefault) admin.WithEnvironment("AdminServer__EventStoreDaprHttpEndpoint", "http://localhost:48123");
            if (scenario != "default-app-id" && !sdkDefault) admin.WithEnvironment("AdminServer__EventStoreAppId", ownerAppId);
            DevelopmentRouteCatalogReceiver[] receivers = [new(gateway, []) { TechnicalHttp = true }, new(domain, [new(gateway, "process", ["POST"])]),
                new(admin, []) { TechnicalHttp = true, ActorStateOwners = [gateway] }];
            if (scenario == "missing-port")
            {
                Should.Throw<InvalidOperationException>(() => builder.AddEventStoreDevelopmentRouteCatalog(gateway,
                    [new(domain, typeof(DevelopmentCatalogProbeDeclarations).Assembly, typeof(DevelopmentCatalogProbeDeclarations).Assembly)], receivers, state, DevelopmentCatalogModelCustody.Create(input)));
                return;
            }
            await using var catalog = builder.AddEventStoreDevelopmentRouteCatalog(gateway,
                [new(domain, typeof(DevelopmentCatalogProbeDeclarations).Assembly, typeof(DevelopmentCatalogProbeDeclarations).Assembly)], receivers, state, DevelopmentCatalogModelCustody.Create(input));
            if (scenario == "late-port")
            {
                var annotation = sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single();
                sidecar.Annotations[sidecar.Annotations.IndexOf(annotation)] = new DaprSidecarOptionsAnnotation(annotation.Options with { DaprHttpPort = 48124 });
            }
            if (scenario == "late-receiver-token") admin.WithEnvironment("EventStore__RouteCatalog__DevelopmentActorStateOwners__0__ApiToken", replacement);
            if (scenario == "late-receiver-endpoint") admin.WithEnvironment("AdminServer__EventStoreDaprHttpEndpoint", "http://127.0.0.1:48123");
            await using var application = builder.Build();
            var eventing = application.Services.GetRequiredService<IDistributedApplicationEventing>();
            foreach (var subscriber in application.Services.GetServices<IDistributedApplicationEventingSubscriber>().Where(subscriber =>
                subscriber.GetType().Name == "DaprDistributedApplicationLifecycleHook" || subscriber is DevelopmentRouteCatalogComponentObserver or DevelopmentRouteCatalogOwnerReadObserver))
                await subscriber.SubscribeAsync(eventing, builder.ExecutionContext, TestContext.Current.CancellationToken);
            async Task Render() => await eventing.PublishAsync(new BeforeStartEvent(application.Services, application.Services.GetRequiredService<DistributedApplicationModel>()), TestContext.Current.CancellationToken);
            if (scenario == "late-port")
            {
                (await Should.ThrowAsync<InvalidOperationException>(Render)).Message.ShouldContain("rendered owner sidecar");
                File.Exists(Path.Combine(catalog.Directory, "admin.actor-state-owners.json")).ShouldBeFalse(); return;
            }
            await Render();
            int allocatedPort = 48200;
            foreach (var resource in application.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<ExecutableResource>())
                foreach (var endpoint in resource.Annotations.OfType<EndpointAnnotation>())
                    endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, scenario == "localhost-allocation" ? "localhost" : scenario == "ipv6-allocation" ? "::1" : "127.0.0.1", endpoint.Port ?? allocatedPort++,
                        targetPortExpression: (endpoint.TargetPort ?? allocatedPort++).ToString(System.Globalization.CultureInfo.InvariantCulture));
            var executable = application.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<ExecutableResource>().Single(resource => resource.Name == sidecar.Name + "-cli");
            if (scenario is "late-argv-id" or "late-argv-port") executable.Annotations.Add(new CommandLineArgsCallbackAnnotation((IList<object> arguments) =>
            {
                string flag = scenario == "late-argv-id" ? "--app-id" : "--dapr-http-port";
                int index = arguments.Select((value, index) => (value, index)).Single(item => item.value is string value && value == flag).index;
                arguments[index + 1] = scenario == "late-argv-id" ? "foreign-owner" : "48124";
            }));
            if (scenario == "late-allocation")
            {
                var endpoint = executable.Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "http");
                endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "127.0.0.1", 48124, targetPortExpression: "48200");
            }
            if (scenario == "wrong-loopback-address")
            {
                var endpoint = executable.Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "http");
                endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "127.0.0.2", 48123, targetPortExpression: endpoint.AllocatedEndpoint!.TargetPortExpression);
            }
            string receiptPath = Path.Combine(catalog.Directory, "admin.actor-state-owners.json");
            string outside = Path.Combine(input, "outside-target.txt");
            if (scenario is "receipt-link" or "existing-receipt")
            {
                await File.WriteAllTextAsync(outside, "preserved-outside-bytes", TestContext.Current.CancellationToken);
                if (scenario == "receipt-link") File.CreateSymbolicLink(receiptPath, outside);
                else await File.WriteAllTextAsync(receiptPath, "preserved-existing-receipt", TestContext.Current.CancellationToken);
            }
            if (scenario == "late-owner-token")
                sidecar.Annotations.Add(new EnvironmentCallbackAnnotation(context => context.EnvironmentVariables["DAPR_API_TOKEN"] = replacement));
            async Task<Dictionary<string, object>> Environment(IResource resource)
            {
                Dictionary<string, object> effective = [];
                foreach (var callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
                    await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, resource, effective, TestContext.Current.CancellationToken));
                return effective;
            }
            if (scenario.StartsWith("late-", StringComparison.Ordinal) || scenario == "wrong-loopback-address")
            {
                var refusal = await Should.ThrowAsync<InvalidOperationException>(() => eventing.PublishAsync(new BeforeResourceStartedEvent(admin.Resource, application.Services), TestContext.Current.CancellationToken));
                refusal.Message.ShouldContain(scenario == "late-owner-token" ? "credential changed after rendering"
                    : scenario == "late-receiver-token" ? "final Admin owner-read handoff" : scenario == "late-receiver-endpoint" ? "final Admin native transport"
                    : scenario is "late-allocation" or "wrong-loopback-address" ? "allocated owner HTTP listener" : "evaluated owner CLI");
                File.Exists(receiptPath).ShouldBeFalse(); return;
            }
            if (scenario is "receipt-link" or "existing-receipt")
            {
                (await Should.ThrowAsync<InvalidOperationException>(() => eventing.PublishAsync(new BeforeResourceStartedEvent(admin.Resource, application.Services), TestContext.Current.CancellationToken)))
                    .Message.ShouldContain("receipt already exists");
                (await File.ReadAllTextAsync(outside, TestContext.Current.CancellationToken)).ShouldBe("preserved-outside-bytes");
                (await File.ReadAllTextAsync(receiptPath, TestContext.Current.CancellationToken)).ShouldBe(scenario == "receipt-link" ? "preserved-outside-bytes" : "preserved-existing-receipt"); return;
            }
            File.Exists(receiptPath).ShouldBeFalse();
            await eventing.PublishAsync(new BeforeResourceStartedEvent(executable, application.Services), TestContext.Current.CancellationToken);
            await eventing.PublishAsync(new BeforeResourceStartedEvent(admin.Resource, application.Services), TestContext.Current.CancellationToken);
            var actual = await Environment(admin.Resource);
            var credential = actual["EventStore__RouteCatalog__DevelopmentActorStateOwners__0__ApiToken"];
            string? token = credential is IValueProvider provider ? await provider.GetValueAsync(TestContext.Current.CancellationToken) : credential.ToString();
            token.ShouldNotBeNullOrWhiteSpace(); if (scenario == "existing") token.ShouldBe(existing);
            actual["EventStore__RouteCatalog__DevelopmentActorStateOwners__0__AppId"].ShouldBe(ownerAppId);
            actual["EventStore__RouteCatalog__DevelopmentActorStateOwners__0__HttpEndpoint"].ShouldBe("http://localhost:48123/");
            actual["AdminServer__EventStoreAppId"].ShouldBe(ownerAppId);
            string receipt = await File.ReadAllTextAsync(Path.Combine(catalog.Directory, "admin.actor-state-owners.json"), TestContext.Current.CancellationToken);
            receipt.ShouldNotContain(token); receipt.ShouldNotContain("ApiToken\"");
            JsonSerializer.Deserialize<DevelopmentActorStateOwnerObservation[]>(receipt).ShouldBe([new(ownerAppId, "http://localhost:48123/", true)]);
            executable.Command.ShouldBe(command); File.Exists(command).ShouldBeFalse();
            var ownerEnvironment = await Environment(gateway.Resource);
            var ownerToken = ownerEnvironment["DAPR_API_TOKEN"];
            (ownerToken is IValueProvider ownerProvider ? await ownerProvider.GetValueAsync(TestContext.Current.CancellationToken) : ownerToken.ToString()).ShouldBe(token);
        }
        finally { Directory.Delete(input, recursive: true); }
    }
}
