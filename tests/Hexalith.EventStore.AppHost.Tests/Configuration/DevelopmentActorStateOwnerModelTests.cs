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
    /// <summary>Serializes only the selected owner's initial credential evaluation and lets independent callers cancel and retry.</summary>
    [Fact(Timeout = 30000)]
    public async Task CanonicalCredentialInitializationIsPerOwnerAndCancellationDoesNotPoisonWaiters()
    {
        string input = Directory.CreateTempSubdirectory("catalog-owner-initialization-").FullName;
        try
        {
            string config = Path.Combine(input, "configuration.json"); string state = Path.Combine(input, "state.json");
            await File.WriteAllTextAsync(config, """{"kind":"Configuration","spec":{"accessControl":{"defaultAction":"deny","trustDomain":"public"}}}""", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(state, """{"kind":"Component","spec":{"type":"state.redis","metadata":[{"name":"redisHost","value":"localhost:16379"}]}}""", TestContext.Current.CancellationToken);
            var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
            builder.Configuration["DcpPublisher:CliPath"] = Path.Combine(input, "model-only-dcp-not-started");
            builder.Configuration["DcpPublisher:DashboardPath"] = Path.Combine(input, "model-only-dashboard-not-started");
            builder.Environment.EnvironmentName = "Development";
            builder.AddDapr(options => options.DaprPath = Path.Combine(input, "model-only-dapr-not-started"));
            IResourceBuilder<ProjectResource> Project(string name, string appId, int port) => builder.AddProject<EventStoreProjectMetadata>(name)
                .WithDaprSidecar(sidecar => sidecar.WithOptions(new DaprSidecarOptions { AppId = appId, Config = config, DaprHttpPort = port }));
            var gateway = Project("gateway", "gateway-owned", 48123);
            var domain = Project("domain", "domain-owned", 48124);
            var admin = Project("admin", "admin-owned", 48125);
            admin.WithEnvironment("AdminServer__EventStoreDaprHttpEndpoint", "http://localhost:48123");
            admin.WithEnvironment("AdminServer__EventStoreAppId", "gateway-owned");
            var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var successorEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            string privateToken = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            int entries = 0;
            gateway.Resource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.Add(new EnvironmentCallbackAnnotation(async context =>
            {
                int entry = Interlocked.Increment(ref entries);
                if (entry == 1) firstEntered.TrySetResult();
                else successorEntered.TrySetResult();
                if (entry <= 2) await release.Task.WaitAsync(context.CancellationToken);
                context.EnvironmentVariables["DAPR_API_TOKEN"] = privateToken;
            }));
            await using var catalog = builder.AddEventStoreDevelopmentRouteCatalog(gateway,
                [new(domain, typeof(DevelopmentCatalogProbeDeclarations).Assembly, typeof(DevelopmentCatalogProbeDeclarations).Assembly)],
                [new(gateway, []) { TechnicalHttp = true }, new(domain, [new(gateway, "process", ["POST"])]),
                 new(admin, []) { TechnicalHttp = true, ActorStateOwners = [gateway, domain] }], state, DevelopmentCatalogModelCustody.Create(input));
            await using var application = builder.Build();
            await using var readiness = await DevelopmentCatalogModelReadiness.StartAsync(application.Services, TestContext.Current.CancellationToken);
            var eventing = application.Services.GetRequiredService<IDistributedApplicationEventing>();
            var notifications = application.Services.GetRequiredService<ResourceNotificationService>();
            var model = application.Services.GetRequiredService<DistributedApplicationModel>();
            foreach (var subscriber in application.Services.GetServices<IDistributedApplicationEventingSubscriber>().Where(subscriber =>
                subscriber.GetType().Name == "DaprDistributedApplicationLifecycleHook" || subscriber is DevelopmentRouteCatalogComponentObserver or DevelopmentRouteCatalogOwnerReadObserver))
                await subscriber.SubscribeAsync(eventing, builder.ExecutionContext, TestContext.Current.CancellationToken);
            await eventing.PublishAsync(new BeforeStartEvent(application.Services, model), TestContext.Current.CancellationToken);
            foreach (var component in model.Resources.OfType<IDaprComponentResource>())
                await notifications.PublishUpdateAsync(component, snapshot => snapshot with { State = KnownResourceStates.Running });
            int allocatedPort = 48200;
            foreach (var executable in model.Resources.OfType<ExecutableResource>())
                foreach (var endpoint in executable.Annotations.OfType<EndpointAnnotation>())
                    endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "127.0.0.1", endpoint.Port ?? allocatedPort++,
                        targetPortExpression: (endpoint.TargetPort ?? allocatedPort++).ToString(System.Globalization.CultureInfo.InvariantCulture));
            async Task<Dictionary<string, object>> Environment(ProjectResource resource, CancellationToken token)
            {
                Dictionary<string, object> environment = [];
                foreach (var callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
                    await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, resource, environment, token));
                return environment;
            }
            using var initializerCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            using var waiterCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            using var successorCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            List<Task> pending = [];
            try
            {
                Task first = Environment(gateway.Resource, initializerCancellation.Token); pending.Add(first);
                await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                Task waiter = Environment(gateway.Resource, waiterCancellation.Token); pending.Add(waiter);
                Volatile.Read(ref entries).ShouldBe(1, "The frozen callback must not enter concurrently for the same owner's first credential.");
                await waiterCancellation.CancelAsync();
                await Should.ThrowAsync<OperationCanceledException>(() => waiter);
                first.IsCompleted.ShouldBeFalse();
                Task<Dictionary<string, object>> successor = Environment(gateway.Resource, successorCancellation.Token); pending.Add(successor);
                Volatile.Read(ref entries).ShouldBe(1);
                await initializerCancellation.CancelAsync();
                await Should.ThrowAsync<OperationCanceledException>(() => first);
                await successorEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                Volatile.Read(ref entries).ShouldBe(2);
                successor.IsCompleted.ShouldBeFalse();
                var independentOwner = await Environment(domain.Resource, TestContext.Current.CancellationToken)
                    .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                independentOwner.ContainsKey("DAPR_API_TOKEN").ShouldBeTrue();
                release.TrySetResult();
                var initialized = await successor.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                initialized["DAPR_API_TOKEN"].ShouldBe(privateToken);
                successorCancellation.IsCancellationRequested.ShouldBeFalse();
                int beforeCachedRead = Volatile.Read(ref entries);
                (await Environment(gateway.Resource, TestContext.Current.CancellationToken))["DAPR_API_TOKEN"].ShouldBe(privateToken);
                Volatile.Read(ref entries).ShouldBe(beforeCachedRead + 1, "A cached successful canonical value only needs current-sidecar mutation validation.");
            }
            finally
            {
                await initializerCancellation.CancelAsync();
                await waiterCancellation.CancelAsync();
                await successorCancellation.CancelAsync();
                release.TrySetResult();
                await Task.WhenAll(pending).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
            }
        }
        finally { Directory.Delete(input, recursive: true); }
    }

    /// <summary>Defers runtime trust inputs until declared dependencies are ready, including concurrent and cancelled starts.</summary>
    /// <param name="cancelFirstStart">Whether to cancel the first waiting start before retrying with a fresh token.</param>
    /// <param name="cliOnlyDependency">Whether only the actual CLI declares the deferred dependency.</param>
    [Theory(Timeout = 30000)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task RuntimeOwnerInputsWaitForDependenciesWithoutBlockingGlobalRendering(bool cancelFirstStart, bool cliOnlyDependency)
    {
        string input = Directory.CreateTempSubdirectory("catalog-owner-readiness-").FullName;
        try
        {
            string config = Path.Combine(input, "configuration.json");
            string state = Path.Combine(input, "state.json");
            string trustPath = Path.Combine(input, "deferred-trust.txt");
            string trustMaterial = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            await File.WriteAllTextAsync(config, """{"kind":"Configuration","spec":{"accessControl":{"defaultAction":"deny","trustDomain":"public"}}}""", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(state, """{"kind":"Component","spec":{"type":"state.redis","metadata":[{"name":"redisHost","value":"localhost:16379"}]}}""", TestContext.Current.CancellationToken);
            var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
            builder.Configuration["DcpPublisher:CliPath"] = Path.Combine(input, "model-only-dcp-not-started");
            builder.Configuration["DcpPublisher:DashboardPath"] = Path.Combine(input, "model-only-dashboard-not-started");
            builder.Environment.EnvironmentName = "Development";
            builder.AddDapr(options => options.DaprPath = Path.Combine(input, "model-only-dapr-not-started"));
            var credentialSource = builder.AddContainer("credential-source", "model-only-image-not-started");
            IResourceBuilder<ProjectResource> Project(string name, string appId, int? port = null)
            {
                var project = builder.AddProject<EventStoreProjectMetadata>(name)
                    .WithDaprSidecar(sidecar => sidecar.WithOptions(new DaprSidecarOptions { AppId = appId, Config = config, DaprHttpPort = port }));
                return cliOnlyDependency ? project : project.WaitFor(credentialSource);
            }
            var gateway = Project("gateway", "gateway-owned", 48123);
            var domain = Project("domain", "domain-owned");
            var admin = Project("admin", "admin-owned");
            admin.WithEnvironment("AdminServer__EventStoreDaprHttpEndpoint", "http://localhost:48123");
            admin.WithEnvironment("AdminServer__EventStoreAppId", "gateway-owned");
            int trustReads = 0;
            foreach (var project in new[] { gateway, domain, admin })
            {
                var sidecar = project.Resource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
                sidecar.Annotations.Add(new EnvironmentCallbackAnnotation(async context =>
                {
                    Interlocked.Increment(ref trustReads);
                    context.EnvironmentVariables["DAPR_TRUST_ANCHORS"] = await File.ReadAllTextAsync(trustPath, context.CancellationToken);
                }));
            }
            await using var catalog = builder.AddEventStoreDevelopmentRouteCatalog(gateway,
                [new(domain, typeof(DevelopmentCatalogProbeDeclarations).Assembly, typeof(DevelopmentCatalogProbeDeclarations).Assembly)],
                [new(gateway, []) { TechnicalHttp = true }, new(domain, [new(gateway, "process", ["POST"])]),
                 new(admin, []) { TechnicalHttp = true, ActorStateOwners = [gateway] }], state, DevelopmentCatalogModelCustody.Create(input));
            await using var application = builder.Build();
            // Native WaitFor readiness includes the completed ResourceReadyEvent. Run only
            // Aspire's readiness monitor; DCP, containers and application hosts stay stopped.
            await using var readiness = await DevelopmentCatalogModelReadiness.StartAsync(application.Services, TestContext.Current.CancellationToken);
            var eventing = application.Services.GetRequiredService<IDistributedApplicationEventing>();
            var notifications = application.Services.GetRequiredService<ResourceNotificationService>();
            var model = application.Services.GetRequiredService<DistributedApplicationModel>();
            foreach (var subscriber in application.Services.GetServices<IDistributedApplicationEventingSubscriber>().Where(subscriber =>
                subscriber.GetType().Name == "DaprDistributedApplicationLifecycleHook" || subscriber is DevelopmentRouteCatalogComponentObserver or DevelopmentRouteCatalogOwnerReadObserver))
                await subscriber.SubscribeAsync(eventing, builder.ExecutionContext, TestContext.Current.CancellationToken);
            await notifications.PublishUpdateAsync(credentialSource.Resource, snapshot => snapshot with { State = KnownResourceStates.Starting });
            await eventing.PublishAsync(new BeforeStartEvent(application.Services, model), TestContext.Current.CancellationToken);
            foreach (var component in model.Resources.OfType<IDaprComponentResource>())
                await notifications.PublishUpdateAsync(component, snapshot => snapshot with { State = KnownResourceStates.Running });
            Volatile.Read(ref trustReads).ShouldBe(0);
            string receiptPath = Path.Combine(catalog.Directory, "admin.actor-state-owners.json");
            File.Exists(receiptPath).ShouldBeFalse();
            int allocatedPort = 48200;
            foreach (var resource in model.Resources.OfType<ExecutableResource>())
                foreach (var endpoint in resource.Annotations.OfType<EndpointAnnotation>())
                    endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "127.0.0.1", endpoint.Port ?? allocatedPort++,
                        targetPortExpression: (endpoint.TargetPort ?? allocatedPort++).ToString(System.Globalization.CultureInfo.InvariantCulture));
            var gatewayExecutable = model.Resources.OfType<ExecutableResource>().Single(resource => resource.Name == "gateway-dapr-cli");
            var adminExecutable = model.Resources.OfType<ExecutableResource>().Single(resource => resource.Name == "admin-dapr-cli");
            if (cliOnlyDependency)
            {
                foreach (var project in new[] { gateway, domain, admin })
                {
                    project.Resource.Annotations.OfType<WaitAnnotation>().ShouldNotContain(wait => ReferenceEquals(wait.Resource, credentialSource.Resource));
                    project.Resource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<WaitAnnotation>()
                        .ShouldNotContain(wait => ReferenceEquals(wait.Resource, credentialSource.Resource));
                    var cli = model.Resources.OfType<ExecutableResource>().Single(resource => resource.Name == project.Resource.Name + "-dapr-cli");
                    cli.Annotations.Add(new WaitAnnotation(credentialSource.Resource, WaitType.WaitUntilHealthy));
                }
            }
            int argumentReads = 0;
            gatewayExecutable.Annotations.Add(new CommandLineArgsCallbackAnnotation(async context =>
            {
                Interlocked.Increment(ref argumentReads);
                _ = await File.ReadAllTextAsync(trustPath, context.CancellationToken);
            }));
            IResource[] resources = [gateway.Resource, gatewayExecutable, admin.Resource, adminExecutable, admin.Resource];
            async Task ReadOwnerEnvironment(CancellationToken token)
            {
                Dictionary<string, object> environment = [];
                foreach (var callback in gateway.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
                    await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, gateway.Resource, environment, token));
            }
            Task[] Start(CancellationToken token) => resources.Select(resource => eventing.PublishAsync(
                new BeforeResourceStartedEvent(resource, application.Services), EventDispatchBehavior.BlockingConcurrent, token)).Append(ReadOwnerEnvironment(token)).ToArray();
            async Task RequireWaiting()
            {
                await Task.WhenAll(resources.Where(resource => !cliOnlyDependency || resource is ExecutableResource).Select(resource => notifications.WaitForResourceAsync(resource.Name,
                    KnownResourceStates.Waiting, TestContext.Current.CancellationToken))).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                Volatile.Read(ref trustReads).ShouldBe(0, $"Argument callback reads: {Volatile.Read(ref argumentReads)}");
                Volatile.Read(ref argumentReads).ShouldBe(0);
                File.Exists(receiptPath).ShouldBeFalse();
            }
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            Task[] starts = Start(cancellation.Token);
            await RequireWaiting();
            starts.ShouldAllBe(start => !start.IsCompleted);
            if (cancelFirstStart)
            {
                await cancellation.CancelAsync();
                foreach (Task start in starts)
                    await Should.ThrowAsync<OperationCanceledException>(() => start);
                Volatile.Read(ref trustReads).ShouldBe(0);
                File.Exists(receiptPath).ShouldBeFalse();
                starts = Start(TestContext.Current.CancellationToken);
                await RequireWaiting();
                starts.ShouldAllBe(start => !start.IsCompleted);
            }
            await File.WriteAllTextAsync(trustPath, trustMaterial, TestContext.Current.CancellationToken);
            await notifications.PublishUpdateAsync(credentialSource.Resource, snapshot => snapshot with { State = KnownResourceStates.Running });
            await Task.WhenAll(starts).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Volatile.Read(ref trustReads).ShouldBeGreaterThan(0);
            Volatile.Read(ref argumentReads).ShouldBeGreaterThan(0);
            Dictionary<string, object> environment = [];
            foreach (var callback in admin.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
                await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, admin.Resource, environment, TestContext.Current.CancellationToken));
            object credential = environment["EventStore__RouteCatalog__DevelopmentActorStateOwners__0__ApiToken"];
            string? token = credential is IValueProvider provider ? await provider.GetValueAsync(TestContext.Current.CancellationToken) : credential.ToString();
            token.ShouldNotBeNullOrWhiteSpace();
            string receipt = await File.ReadAllTextAsync(receiptPath, TestContext.Current.CancellationToken);
            receipt.ShouldNotContain(token);
            receipt.ShouldNotContain(trustMaterial);
            receipt.ShouldNotContain("ApiToken\"");
            JsonSerializer.Deserialize<DevelopmentActorStateOwnerObservation[]>(receipt).ShouldBe([new("gateway-owned", "http://localhost:48123/", true)]);
            foreach (string name in new[] { "authority.backend.json", "authority.dependencies.json", "authority.component.json" })
            {
                string componentReceipt = await File.ReadAllTextAsync(Path.Combine(catalog.Directory, name), TestContext.Current.CancellationToken);
                componentReceipt.ShouldNotContain(token);
                componentReceipt.ShouldNotContain(trustMaterial);
            }
        }
        finally { Directory.Delete(input, recursive: true); }
    }

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
            await using var readiness = await DevelopmentCatalogModelReadiness.StartAsync(application.Services, TestContext.Current.CancellationToken);
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
            var notifications = application.Services.GetRequiredService<ResourceNotificationService>();
            foreach (var component in application.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<IDaprComponentResource>())
                await notifications.PublishUpdateAsync(component, snapshot => snapshot with { State = KnownResourceStates.Running });
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
