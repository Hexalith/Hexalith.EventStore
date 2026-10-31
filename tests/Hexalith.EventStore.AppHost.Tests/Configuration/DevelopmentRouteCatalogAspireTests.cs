using System.Text.Json;
using System.Text.Json.Nodes;

using global::Aspire.Hosting;
using global::Aspire.Hosting.ApplicationModel;
using global::Aspire.Hosting.Eventing;
using global::Aspire.Hosting.Lifecycle;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.EventStore.Aspire;
using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.AppHost.Tests.Configuration;

/// <summary>Checks actual Aspire annotations and rendered inputs without starting a shared Dapr topology.</summary>
public sealed class DevelopmentRouteCatalogAspireTests
{
    /// <summary>Preserves owner route bytes and binds existing caller declarations without introducing a caller or grant.</summary>
    [Theory(Timeout = 30000)]
    [InlineData("valid")]
    [InlineData("missing-caller")]
    [InlineData("default-open")]
    [InlineData("deny-override")]
    public async Task ExistingReceiverCaptureRetainsExactClosedOwnerGrantAsync(string scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        string directory = Directory.CreateTempSubdirectory("catalog-existing-policy-").FullName;
        string path = Path.Combine(directory, "api.json");
        byte[] original = JsonSerializer.SerializeToUtf8Bytes(new
        {
            spec = new { accessControl = new { defaultAction = scenario == "default-open" ? "allow" : "deny", trustDomain = "public",
                policies = new[] { new { appId = "ui", defaultAction = "deny", operations = new[] {
                    new { name = "/api/tenants/**/users", action = scenario == "deny-override" ? "deny" : "allow", httpVerb = new[] { "GET" } } } } } } },
        });
        try
        {
            await File.WriteAllBytesAsync(path, original, TestContext.Current.CancellationToken);
            var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
            IResourceBuilder<ProjectResource> Project(string name, string id) => builder.AddProject<EventStoreProjectMetadata>(name)
                .WithDaprSidecar(sidecar => sidecar.WithOptions(new DaprSidecarOptions { AppId = id, Config = path }));
            var api = Project("api", "api-owned"); var ui = Project("ui", "ui-owned");
            if (scenario == "valid")
            {
                var captured = api.CaptureEventStoreDevelopmentReceiver(ui).Invocations.ShouldHaveSingleItem();
                captured.Caller.ShouldBeSameAs(ui); captured.Method.ShouldBe("/api/tenants/**/users"); captured.HttpMethods.ShouldBe(["GET"]);
            }
            else Should.Throw<InvalidOperationException>(() => api.CaptureEventStoreDevelopmentReceiver(scenario == "missing-caller" ? [] : [ui]));
            (await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken)).ShouldBe(original);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>Uses the physical sidecar identities consistently while retaining noncohort readiness and private ownership.</summary>
    [Theory(Timeout = 30000)]
    [InlineData("valid")]
    [InlineData("sdk-backend")]
    [InlineData("late-backend")]
    [InlineData("late-authority-prefix")]
    [InlineData("late-authority-scope")]
    [InlineData("technical")]
    [InlineData("production")]
    [InlineData("empty-owners")]
    [InlineData("missing-gateway")]
    [InlineData("duplicate-receiver")]
    [InlineData("unselected-caller")]
    [InlineData("duplicate-grant")]
    public async Task ActualResourcesOwnCanonicalCatalogFiniteAclAndPrivateAuthorityAsync(string scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        string input = Path.Combine(Path.GetTempPath(), "catalog-model-input-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(input);
        string config = Path.Combine(input, "configuration.yaml"); string state = Path.Combine(input, "state.yaml");
        await File.WriteAllTextAsync(config, "apiVersion: dapr.io/v1alpha1\nkind: Configuration\nmetadata:\n  name: catalog-model\nspec:\n  mtls:\n    enabled: true\n  accessControl:\n    defaultAction: deny\n    trustDomain: public\n", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(state, "apiVersion: dapr.io/v1alpha1\nkind: Component\nmetadata:\n  name: statestore\nspec:\n  type: state.redis\n  version: v1\n  metadata:\n    - name: redisHost\n      value: localhost:16379\n    - name: actorStateStore\n      value: 'true'\n    - name: keyPrefix\n      value: appid\n", TestContext.Current.CancellationToken);
        string? privateDirectory = null;
        try
        {
            var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true, AllowUnsecuredTransport = true });
            builder.Configuration["DcpPublisher:CliPath"] = Path.Combine(input, "model-only-dcp-not-started");
            builder.Configuration["DcpPublisher:DashboardPath"] = Path.Combine(input, "model-only-dashboard-not-started");
            // This test invokes the toolkit renderer, never DCP or a CLI process. An explicit
            // nonexistent command keeps the model independent of an installed Dapr CLI.
            string modeledDaprPath = Path.Combine(input, "model-only-dapr-not-started");
            builder.AddDapr(options => options.DaprPath = modeledDaprPath);
            builder.Environment.EnvironmentName = "Development";
            IResourceBuilder<ProjectResource> Project(string name, string appId) => builder.AddProject<EventStoreProjectMetadata>(name)
                .WithDaprSidecar(sidecar => sidecar.WithOptions(new DaprSidecarOptions { AppId = appId, Config = config }));
            HexalithEventStoreResources? sdk = null;
            var gateway = scenario == "sdk-backend" ? builder.AddProject<EventStoreProjectMetadata>("eventstore") : Project("eventstore", "eventstore-owned-session");
            if (scenario == "sdk-backend")
            {
                sdk = builder.AddHexalithEventStore(gateway, null);
                var sidecar = Sidecar(gateway); var original = sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single();
                sidecar.Annotations[sidecar.Annotations.IndexOf(original)] = new DaprSidecarOptionsAnnotation(original.Options with { AppId = "eventstore-owned-session", Config = config });
            }
            var domain = Project("domain", "domain-owned-session");
            var proxy = Project("ui", "ui-owned-session"); var infrastructure = builder.AddRedis("cache");
            domain.WaitFor(gateway).WaitFor(infrastructure); proxy.WaitFor(domain);
            DevelopmentRouteCatalogOwner[] owners = [new(domain, typeof(DevelopmentCatalogProbeDeclarations).Assembly, typeof(DevelopmentCatalogProbeDeclarations).Assembly)];
            DevelopmentRouteCatalogInvocation[] grants = [new(gateway, "process", ["POST"])];
            if (scenario == "production") builder.Environment.EnvironmentName = "Production";
            if (scenario == "empty-owners") owners = [];
            if (scenario == "unselected-caller") grants = [new(builder.AddProject<EventStoreProjectMetadata>("no-sidecar"), "process", ["POST"])];
            if (scenario == "duplicate-grant") grants = [grants[0], grants[0]];
            DevelopmentRouteCatalogReceiver[] receivers = [new(gateway, []) { TechnicalHttp = scenario == "technical" }, new(domain, grants)];
            if (scenario == "missing-gateway") receivers = [receivers[1]];
            if (scenario == "duplicate-receiver") receivers = [.. receivers, receivers[1]];
            if (scenario is not ("valid" or "sdk-backend" or "technical" or "late-backend" or "late-authority-prefix" or "late-authority-scope"))
            {
                Should.Throw<InvalidOperationException>(() => builder.AddEventStoreDevelopmentRouteCatalog(gateway, owners, receivers, state, DevelopmentCatalogModelCustody.Create(input), "owned-session"));
                builder.Resources.OfType<ParameterResource>().ShouldBeEmpty();
                return;
            }
            await using (var catalog = sdk is not null ? builder.AddEventStoreDevelopmentRouteCatalog(gateway, owners, receivers, sdk.StateStore, DevelopmentCatalogModelCustody.Create(input), "owned-session")
                : builder.AddEventStoreDevelopmentRouteCatalog(gateway, owners, receivers, state, DevelopmentCatalogModelCustody.Create(input), "owned-session"))
            {
                privateDirectory = catalog.Directory;
                RouteCatalogEnvelope decoded = RouteCatalogCodec.Decode(await File.ReadAllBytesAsync(Path.Combine(catalog.Directory, "catalog.json"), TestContext.Current.CancellationToken), catalog.RootDigest);
                decoded.Routes.ShouldHaveSingleItem().AppId.ShouldBe("domain-owned-session");
                var policy = decoded.Idempotency.ShouldHaveSingleItem(); policy.ConsumerAppId.ShouldBe("eventstore-owned-session");
                policy.ContentDigest.ShouldBe(RouteCatalogCodec.ComputeIdempotencyEntryDigest(policy));
                DevelopmentIdempotencyBindings.Bind(DevelopmentCatalogProbeDeclarations.Commands[0], 1).ConsumerAppId.ShouldBe("eventstore");
                var modeledAuthority = builder.Resources.OfType<DaprComponentResource>().Single(resource => resource.Name.StartsWith("routing-", StringComparison.Ordinal));
                modeledAuthority.Options?.LocalPath.ShouldBeNull();
                var endpoint = infrastructure.Resource.Annotations.OfType<EndpointAnnotation>().Single(annotation => annotation.Name == "tcp");
                endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "127.0.0.1", 46379);
                var password = builder.AddParameter("late-private-redis-password", string.Empty, secret: true);
                builder.CreateResourceBuilder<IDaprComponentResource>(modeledAuthority.Name)
                    .WithMetadata("redisHost", ReferenceExpression.Create($"{infrastructure.GetEndpoint("tcp").Property(EndpointProperty.HostAndPort)}"))
                    .WithMetadata("redisPassword", password.Resource).WithMetadata("keyPrefix", "none").WithMetadata("actorStateStore", "true");
                await using var application = builder.Build();
                var eventing = application.Services.GetRequiredService<IDistributedApplicationEventing>();
                foreach (var subscriber in application.Services.GetServices<IDistributedApplicationEventingSubscriber>().Where(subscriber =>
                    subscriber.GetType().Name == "DaprDistributedApplicationLifecycleHook" || subscriber is DevelopmentRouteCatalogComponentObserver))
                    await subscriber.SubscribeAsync(eventing, builder.ExecutionContext, TestContext.Current.CancellationToken);
                await eventing.PublishAsync(new BeforeStartEvent(application.Services, application.Services.GetRequiredService<DistributedApplicationModel>()), TestContext.Current.CancellationToken);
                var observedAuthority = application.Services.GetServices<IDistributedApplicationEventingSubscriber>().OfType<DevelopmentRouteCatalogComponentObserver>().Single();
                string authorityPath = observedAuthority.ComponentPath.ShouldNotBeNull();
                using var component = JsonDocument.Parse(await File.ReadAllBytesAsync(authorityPath, TestContext.Current.CancellationToken));
                component.RootElement.GetProperty("scopes").EnumerateArray().Select(value => value.GetString()).Order(StringComparer.Ordinal)
                    .ShouldBe(["domain-owned-session", "eventstore-owned-session"]);
                var actualMetadata = component.RootElement.GetProperty("spec").GetProperty("metadata").EnumerateArray().ToArray();
                actualMetadata.Single(item => item.GetProperty("name").GetString() == "keyPrefix").GetProperty("value").GetString().ShouldBe("name");
                actualMetadata.ShouldNotContain(item => item.GetProperty("name").GetString() == "actorStateStore");
                string redisReference = actualMetadata.Single(item => item.GetProperty("name").GetString() == "redisHost").GetProperty("secretKeyRef").GetProperty("name").GetString()!;
                File.ReadAllText(authorityPath).ShouldNotContain("localhost:16379");
                File.ReadAllText(authorityPath).ShouldNotContain("localhost:6379");
                int allocatedPort = 48000;
                foreach (var executable in application.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<ExecutableResource>())
                {
                    foreach (var listener in executable.Annotations.OfType<EndpointAnnotation>()) listener.AllocatedEndpoint = new AllocatedEndpoint(listener, "127.0.0.1", allocatedPort++);
                    if (executable.Name != "eventstore-dapr-cli") continue;
                    executable.Command.ShouldBe(modeledDaprPath);
                    File.Exists(executable.Command).ShouldBeFalse();
                    Dictionary<string, object> effective = [];
                    foreach (var callback in executable.Annotations.OfType<EnvironmentCallbackAnnotation>())
                        await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, executable, effective, TestContext.Current.CancellationToken));
                    effective[redisReference].ShouldBe("127.0.0.1:46379");
                }
                if (scenario is "late-authority-prefix" or "late-authority-scope")
                {
                    JsonObject changed = JsonNode.Parse(File.ReadAllBytes(authorityPath))!.AsObject();
                    if (scenario == "late-authority-prefix") changed["spec"]!["metadata"]!.AsArray().Single(item => item!["name"]!.GetValue<string>() == "keyPrefix")!["value"] = "none";
                    else changed["scopes"]!.AsArray().Add("foreign-owner");
                    await File.WriteAllBytesAsync(authorityPath, JsonSerializer.SerializeToUtf8Bytes(changed), TestContext.Current.CancellationToken);
                    (await Should.ThrowAsync<InvalidOperationException>(() => eventing.PublishAsync(new BeforeResourceStartedEvent(gateway.Resource, application.Services), TestContext.Current.CancellationToken)))
                        .Message.ShouldContain("before resource execution");
                    File.Exists(Path.Combine(catalog.Directory, "authority.backend.json")).ShouldBeFalse();
                    return;
                }
                await eventing.PublishAsync(new BeforeResourceStartedEvent(gateway.Resource, application.Services), TestContext.Current.CancellationToken);
                if (scenario == "late-backend")
                {
                    string backendReceipt = Path.Combine(catalog.Directory, "authority.backend.json"); byte[] previous = File.ReadAllBytes(backendReceipt);
                    var cli = application.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<ExecutableResource>().Single(resource => resource.Name == "eventstore-dapr-cli");
                    cli.Annotations.Add(new EnvironmentCallbackAnnotation(context => context.EnvironmentVariables[redisReference] = "127.0.0.1:46380"));
                    (await Should.ThrowAsync<InvalidOperationException>(() => eventing.PublishAsync(new BeforeResourceStartedEvent(gateway.Resource, application.Services), TestContext.Current.CancellationToken)))
                        .Message.ShouldContain("backend changed");
                    File.ReadAllBytes(backendReceipt).ShouldBe(previous);
                    return;
                }
                using var acl = JsonDocument.Parse(await File.ReadAllBytesAsync(Sidecar(domain).Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options.Config!, TestContext.Current.CancellationToken));
                var incoming = acl.RootElement.GetProperty("spec").GetProperty("accessControl").GetProperty("policies").EnumerateArray().Single();
                incoming.GetProperty("appId").GetString().ShouldBe("eventstore-owned-session"); incoming.GetProperty("namespace").GetString().ShouldBe("owned-session");
                incoming.GetProperty("operations").EnumerateArray().Single().GetProperty("name").GetString().ShouldBe("process");
                acl.RootElement.GetProperty("spec").GetProperty("mtls").GetProperty("enabled").GetBoolean().ShouldBeTrue();
                domain.Resource.Annotations.OfType<WaitAnnotation>().Single(wait => ReferenceEquals(wait.Resource, gateway.Resource)).WaitType.ShouldBe(WaitType.WaitUntilStarted);
                domain.Resource.Annotations.OfType<WaitAnnotation>().Single(wait => ReferenceEquals(wait.Resource, infrastructure.Resource)).WaitType.ShouldBe(WaitType.WaitUntilHealthy);
                proxy.Resource.Annotations.OfType<WaitAnnotation>().Single().WaitType.ShouldBe(WaitType.WaitUntilHealthy);
                Dictionary<string, IConfigurationRoot> configurations = [];
                IReadOnlyList<IdempotencyCatalogEntry> installedAdmission = [];
                RouteCatalogStateCoordinates? actualCoordinates = null;
                foreach (var project in new[] { gateway, domain })
                {
                    Dictionary<string, object> environment = [];
                    foreach (var callback in project.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
                        await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, project.Resource, environment, TestContext.Current.CancellationToken));
                    environment["ASPNETCORE_ENVIRONMENT"].ShouldBe("Development");
                    if (scenario == "technical") environment["EventStore__RouteCatalog__DevelopmentBootstrap__BindingVersion"].ShouldBe("2");
                    else environment.ShouldNotContainKey("EventStore__RouteCatalog__DevelopmentBootstrap__BindingVersion");
                    environment["EventStore__RouteCatalog__DevelopmentBootstrap__AppId"].ShouldBe(project == gateway ? "eventstore-owned-session" : "domain-owned-session");
                    environment["APP_API_TOKEN"].ShouldBeOfType<ParameterResource>().Secret.ShouldBeTrue();
                    environment["EventStore__RouteCatalog__DevelopmentBootstrap__ComponentPath"].ShouldBe(authorityPath);
                    Dictionary<string, string?> effective = [];
                    foreach (var item in environment)
                        effective[item.Key.Replace("__", ":", StringComparison.Ordinal)] = item.Value is ParameterResource value
                            ? await value.GetValueAsync(TestContext.Current.CancellationToken) : item.Value.ToString();
                    // DCP allocation is modeled above. Resolve this one actual sidecar HTTP
                    // coordinate without waiting on unrelated dashboard/gRPC expressions.
                    effective["DAPR_HTTP_ENDPOINT"] = "http://127.0.0.1:" + effective["DAPR_HTTP_PORT"];
                    var configuration = new ConfigurationBuilder().AddInMemoryCollection(effective).Build();
                    configurations[project.Resource.Name] = configuration;
                    if (project == gateway)
                    {
                        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton(builder.Environment); services.AddEventStoreServer(configuration);
                        services.AddEventStoreRunScopedCatalogGateway(configuration); // historical explicit call remains idempotent
                        services.Count(descriptor => descriptor.ServiceType == typeof(IIdempotencyIntentAdapter)).ShouldBe(1);
                        using var provider = services.BuildServiceProvider();
                        var installed = await RouteCatalogAdmissionObservation.CaptureAsync(provider, "eventstore-owned-session", TestContext.Current.CancellationToken);
                        installed.ShouldHaveSingleItem().ContentDigest.ShouldBe(policy.ContentDigest);
                        installed[0].ConsumerAppId.ShouldBe("eventstore-owned-session");
                        installedAdmission = installed;
                        actualCoordinates = provider.GetRequiredService<IRouteCatalogStateStore>().Coordinates;
                    }
                }
                await AssertActualRenderedMutationRefusedAsync(authorityPath, decoded, configurations, installedAdmission,
                    actualCoordinates.ShouldNotBeNull(), builder.Environment);
                Sidecar(proxy).Annotations.OfType<DaprComponentReferenceAnnotation>().ShouldBeEmpty();
                gateway.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>().ShouldNotBeEmpty();
                if (!OperatingSystem.IsWindows()) (File.GetUnixFileMode(catalog.Directory) & (UnixFileMode.GroupRead | UnixFileMode.OtherRead)).ShouldBe((UnixFileMode)0);
            }
            Directory.Exists(privateDirectory).ShouldBeFalse();
            Directory.Exists(input).ShouldBeTrue();
        }
        finally { Directory.Delete(input, recursive: true); }
    }

    private static IDaprSidecarResource Sidecar(IResourceBuilder<ProjectResource> project) => project.Resource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;

    private static async Task AssertActualRenderedMutationRefusedAsync(string path, RouteCatalogEnvelope catalog,
        IReadOnlyDictionary<string, IConfigurationRoot> configurations, IReadOnlyList<IdempotencyCatalogEntry> admission,
        RouteCatalogStateCoordinates coordinates, Microsoft.Extensions.Hosting.IHostEnvironment environment)
    {
        var declarations = DevelopmentCatalogProbeDeclarations.Routes;
        async Task<RouteCatalogHostObservation> Capture(string host)
        {
            var configuration = configurations[host];
            string app = configuration["EventStore:RouteCatalog:DevelopmentBootstrap:AppId"]!;
            RouteCatalogEntry[] targets = host == "domain" ? declarations.Select(declaration => new RouteCatalogEntry(declaration.RouteId, 1,
                RouteCatalogCodec.ComputeDeclarationDigest(declaration), app, DevelopmentCatalogProbeDeclarations.ReceiverMethods[declaration.RouteId],
                declaration.ContractVersion, declaration.CredentialKind, declaration.Operation, false, "*")).ToArray() : [];
            var observed = await DevelopmentRouteCatalogInputs.CaptureAsync(configuration, app, declarations, targets, host == "eventstore" ? admission : [], [], [], TestContext.Current.CancellationToken);
            return observed with { AuthorityCoordinates = coordinates, Operations = DevelopmentCatalogProbeDeclarations.Operations };
        }
        var gateway = await Capture("eventstore"); var domain = await Capture("domain");
        var identity = ObservedRouteCatalogHost.Identity(gateway);
        var options = Options.Create(new RouteCatalogHostOptions { Identity = identity, Profile = "Development", DevelopmentFixture = true });
        var store = new DevelopmentCatalogModelStateStore(coordinates);
        var authority = new RouteCatalogAuthority(store, new DevelopmentRouteCatalogTrustValidator(options, environment), options,
            new ObservedRouteCatalogHost(_ => Capture("eventstore")));
        var candidate = new RouteCatalogActivation("model-rendered-input", RouteCatalogCodec.Encode(catalog), RouteCatalogCodec.ComputeRootDigest(catalog), null,
            [identity, ObservedRouteCatalogHost.Identity(domain)], "Development-fixture", string.Empty);
        candidate = candidate with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(candidate) };
        await authority.PrepareAsync(candidate, TestContext.Current.CancellationToken);
        await authority.ReadyAsync(candidate.AttemptId, TestContext.Current.CancellationToken);
        byte[] original = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        foreach (string fault in new[] { "backend", "prefix", "scope" })
        {
            JsonObject changed = JsonNode.Parse(original)!.AsObject();
            var metadata = changed["spec"]!["metadata"]!.AsArray();
            if (fault == "backend") metadata.Single(item => item!["name"]!.GetValue<string>() == "redisHost")!["secretKeyRef"]!["name"] = "different-effective-backend";
            if (fault == "prefix") metadata.Single(item => item!["name"]!.GetValue<string>() == "keyPrefix")!["value"] = "none";
            if (fault == "scope") changed["scopes"]!.AsArray().Add("unrelated-caller");
            string before = (await store.ReadAsync(TestContext.Current.CancellationToken)).ETag;
            try
            {
                await File.WriteAllBytesAsync(path, JsonSerializer.SerializeToUtf8Bytes(changed), TestContext.Current.CancellationToken);
                await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.ReadyAsync(candidate.AttemptId, TestContext.Current.CancellationToken));
                (await store.ReadAsync(TestContext.Current.CancellationToken)).ETag.ShouldBe(before);
            }
            finally { await File.WriteAllBytesAsync(path, original, TestContext.Current.CancellationToken); }
        }
    }
}
