using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using YamlDotNet.Serialization;

namespace Hexalith.EventStore.Aspire;

/// <summary>Opt-in, local Development catalog composition using real compiled owners and independently observing SDK hosts.</summary>
public static class DevelopmentRouteCatalogAspireExtensions
{
    /// <summary>Retains a receiver's existing closed allow inventory, binding each explicitly selected caller to its actual resource.</summary>
    public static DevelopmentRouteCatalogReceiver CaptureEventStoreDevelopmentReceiver(this IResourceBuilder<ProjectResource> project,
        params IResourceBuilder<ProjectResource>[] callers)
    {
        ArgumentNullException.ThrowIfNull(project); ArgumentNullException.ThrowIfNull(callers);
        var options = Sidecar(project.Resource).Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
        var acl = ReadYamlOrJson(options.Config ?? throw new InvalidOperationException("An actual receiver configuration is required."))["spec"]!["accessControl"]!;
        if (acl["defaultAction"]?.GetValue<string>() != "deny") throw new InvalidOperationException("Only an existing closed receiver policy can be retained.");
        List<DevelopmentRouteCatalogInvocation> invocations = [];
        foreach (var policy in acl["policies"]!.AsArray())
        {
            string callerId = policy!["appId"]!.GetValue<string>();
            var selected = callers.Where(caller => caller.Resource.Name == callerId || AppId(caller.Resource) == callerId).ToArray();
            if (selected.Length != 1 || policy["defaultAction"]?.GetValue<string>() != "deny")
                throw new InvalidOperationException("An existing receiver caller lacks one independently selected resource.");
            foreach (var grant in policy["operations"]!.AsArray())
            {
                if (grant!["action"]?.GetValue<string>() != "allow") throw new InvalidOperationException("Mixed allow/deny rules require an explicit owner traffic declaration.");
                invocations.Add(new(selected[0], grant["name"]!.GetValue<string>(), grant["httpVerb"]!.AsArray().Select(value => value!.GetValue<string>()).ToArray()));
            }
        }
        return new(project, invocations);
    }

    /// <summary>Creates a complete canonical generation and binds the exact cohort to run-private authority and finite owner traffic.</summary>
    /// <remarks>Call after sidecar identity selection and before Build. Dispose the returned ownership after the application.
    /// Hosted publication is unsupported. This does not grant authority access to human-only proxy callers.</remarks>
    public static DevelopmentRouteCatalogComposition AddEventStoreDevelopmentRouteCatalog(
        this IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> gateway,
        IReadOnlyList<DevelopmentRouteCatalogOwner> owners, IReadOnlyList<DevelopmentRouteCatalogReceiver> receivers,
        string stateStoreComponentPath, string daprNamespace = "default")
        => Compose(builder, gateway, owners, receivers, ReadYamlOrJson(stateStoreComponentPath), daprNamespace);

    /// <summary>Uses the backend selected by the actual SDK state-store model, with ordinary modeled late overrides preserved.</summary>
    public static DevelopmentRouteCatalogComposition AddEventStoreDevelopmentRouteCatalog(
        this IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> gateway,
        IReadOnlyList<DevelopmentRouteCatalogOwner> owners, IReadOnlyList<DevelopmentRouteCatalogReceiver> receivers,
        IResourceBuilder<IDaprComponentResource> stateStore, string daprNamespace = "default")
    {
        ArgumentNullException.ThrowIfNull(stateStore);
        var backend = stateStore.Resource.Annotations.OfType<EventStoreRedisBackendAnnotation>().SingleOrDefault()
            ?? throw new InvalidOperationException("An SDK-selected state backend or explicit owner component input is required.");
        if (stateStore.Resource.Type != "state.redis") throw new InvalidOperationException("The selected SDK state backend is not Redis.");
        JsonObject component = backend.ComponentPath is { } path ? ReadYamlOrJson(path) : new JsonObject
        {
            ["kind"] = "Component", ["spec"] = new JsonObject { ["type"] = "state.redis", ["metadata"] = new JsonArray(
                new JsonObject { ["name"] = "redisHost", ["value"] = backend.DefaultRedisHost }) },
        };
        return Compose(builder, gateway, owners, receivers, component, daprNamespace);
    }

    private static DevelopmentRouteCatalogComposition Compose(IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> gateway,
        IReadOnlyList<DevelopmentRouteCatalogOwner> owners, IReadOnlyList<DevelopmentRouteCatalogReceiver> receivers,
        JsonObject stateComponent, string daprNamespace)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(owners);
        ArgumentNullException.ThrowIfNull(receivers);
        if (!builder.ExecutionContext.IsRunMode || !builder.Environment.IsDevelopment())
            throw new InvalidOperationException("Catalog composition requires an explicit Development run.");
        if (owners.Count == 0 || receivers.Count == 0 || string.IsNullOrWhiteSpace(daprNamespace)
            || receivers.Select(receiver => receiver.Project.Resource).Distinct().Count() != receivers.Count
            || !receivers.Any(receiver => ReferenceEquals(receiver.Project.Resource, gateway.Resource))
            || owners.Any(owner => !receivers.Any(receiver => ReferenceEquals(receiver.Project.Resource, owner.Project.Resource)))
            || owners.Select(owner => owner.Project.Resource).Distinct().Count() != owners.Count)
            throw new InvalidOperationException("An exact gateway, owner and receiver cohort is required.");
        var selected = builder.Resources.OfType<ProjectResource>().Where(project => project.Annotations.OfType<DaprSidecarAnnotation>().Any()).ToArray();
        var apps = selected.ToDictionary(project => project, project => AppId(project));
        if (apps.Values.Distinct(StringComparer.Ordinal).Count() != apps.Count
            || receivers.Any(receiver => !apps.ContainsKey(receiver.Project.Resource))
            || receivers.SelectMany(receiver => receiver.Invocations).Any(invocation => !apps.ContainsKey(invocation.Caller.Resource)))
            throw new InvalidOperationException("Every receiver and declared caller must have one actual selected Dapr identity.");
        var invocations = receivers.ToDictionary(receiver => receiver.Project.Resource, receiver => receiver.Invocations
            .Select(invocation => new RouteCatalogDeclaredInvocation(apps[invocation.Caller.Resource], invocation.Method, invocation.HttpMethods)).ToArray());
        if (invocations.Values.Any(grants => grants.Select(grant => (grant.CallerAppId, grant.Method)).Distinct().Count() != grants.Length
            || grants.Any(grant => string.IsNullOrWhiteSpace(grant.Method) || grant.HttpMethods.Count == 0
                || grant.HttpMethods.Any(verb => verb is not ("GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS")))))
            throw new InvalidOperationException("Duplicate or incomplete owner invocation declarations are not admitted.");
        string[] hosts = receivers.Select(receiver => receiver.Project.Resource.Name).Order(StringComparer.Ordinal).ToArray();
        string gatewayAppId = apps[gateway.Resource];
        var catalog = BuildCatalog(gatewayAppId, owners, apps);
        string root = RouteCatalogCodec.ComputeRootDigest(catalog);
        string instance = Guid.NewGuid().ToString("N");
        string directory = Directory.CreateTempSubdirectory("hexalith-route-catalog-").FullName;
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try
        {
            Write(directory, "catalog.json", RouteCatalogCodec.Encode(catalog));
            Write(directory, "receipt.json", JsonSerializer.SerializeToUtf8Bytes(new { instanceId = instance, rootDigest = root }));
            string componentName = "routing-" + instance;
            JsonObject component = stateComponent.DeepClone().AsObject();
            if (component["kind"]?.GetValue<string>() != "Component" || component["spec"]?["type"]?.GetValue<string>() != "state.redis")
                throw new InvalidOperationException("The actual shared Redis component is required.");
            var metadata = component["spec"]!["metadata"]!.AsArray();
            foreach (var item in metadata.Where(item => item?["name"]?.GetValue<string>() is "actorStateStore" or "keyPrefix").ToArray()) metadata.Remove(item);
            var authority = builder.AddDaprComponent(componentName, "state.redis").WithMetadata("keyPrefix", "name");
            foreach (var item in metadata)
            {
                if (item?["value"] is not JsonValue value || !value.TryGetValue<string>(out string? literal))
                    throw new InvalidOperationException("Initial Redis metadata must be literal; dynamic or secret values belong to modeled metadata parameters.");
                authority.WithMetadata(item!["name"]!.GetValue<string>(), literal);
            }
            var observer = new DevelopmentRouteCatalogComponentObserver(authority.Resource, receivers.Select(receiver => receiver.Project.Resource).ToArray(), apps,
                receivers.ToDictionary(receiver => receiver.Project.Resource, receiver => Path.Combine(directory, receiver.Project.Resource.Name + ".configuration.json")));
            builder.Services.AddSingleton<IDistributedApplicationEventingSubscriber>(observer);
            string digestKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var key = builder.AddParameter("catalog-" + instance + "-digest-key", () => digestKey, secret: true);
            foreach (var receiver in receivers)
            {
                var project = receiver.Project;
                var sidecar = Sidecar(project.Resource);
                var annotation = sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single();
                JsonObject configuration = ReadYamlOrJson(annotation.Options.Config ?? throw new InvalidOperationException("An actual receiver Dapr configuration is required."));
                string trustDomain = configuration["spec"]?["accessControl"]?["trustDomain"]?.GetValue<string>()
                    ?? throw new InvalidOperationException("The receiver must select its Dapr trust domain.");
                var grants = invocations[project.Resource];
                configuration["spec"]!["accessControl"] = JsonSerializer.SerializeToNode(new
                {
                    defaultAction = "deny", trustDomain,
                    policies = grants.GroupBy(grant => grant.CallerAppId, StringComparer.Ordinal).Select(group => new
                    {
                        appId = group.Key, defaultAction = "deny", trustDomain, @namespace = daprNamespace,
                        operations = group.Select(grant => new { name = grant.Method, httpVerb = grant.HttpMethods, action = "allow" }).ToArray(),
                    }).ToArray(),
                });
                string aclPath = Write(directory, project.Resource.Name + ".configuration.json", JsonSerializer.SerializeToUtf8Bytes(configuration));
                sidecar.Annotations[sidecar.Annotations.IndexOf(annotation)] = new DaprSidecarOptionsAnnotation(annotation.Options with { Config = aclPath });
                builder.CreateResourceBuilder(sidecar).WithReference(authority);
                project.WithGeneratedEventStoreAppChannelToken();
                project.WithEnvironment("ASPNETCORE_ENVIRONMENT", Environments.Development).WithEnvironment("DOTNET_ENVIRONMENT", Environments.Development);
                void Set(string name, string value) => project.WithEnvironment("EventStore__RouteCatalog__DevelopmentBootstrap__" + name, value);
                Set("Directory", directory); Set("HostId", project.Resource.Name); Set("AppId", apps[project.Resource]);
                Set("CoordinatorHostId", gateway.Resource.Name); Set("AttemptId", instance); Set("InstanceId", instance);
                Set("AuthorityRole", RouteCatalogAuthorityComponentPolicy.IsolatedRole);
                Set("CatalogPath", Path.Combine(directory, "catalog.json")); Set("RootDigest", root);
                project.WithEnvironment(context => context.EnvironmentVariables["EventStore__RouteCatalog__DevelopmentBootstrap__ComponentPath"] = observer.ComponentPath
                    ?? throw new InvalidOperationException("The actual modeled authority has not been rendered."));
                Set("AccessControlPath", aclPath);
                Set("DeclaredInvocations", JsonSerializer.Serialize(grants));
                for (int i = 0; i < hosts.Length; i++) Set("RequiredHostIds__" + i, hosts[i]);
                string[] selectedApps = apps.Values.Order(StringComparer.Ordinal).ToArray();
                for (int i = 0; i < selectedApps.Length; i++) Set("SelectedAppIds__" + i, selectedApps[i]);
                project.WithEnvironment("EventStore__RouteCatalog__StateStore", componentName)
                    .WithEnvironment("EventStore__RouteCatalog__StateKey", instance + "-authority")
                    .WithEnvironment("EventStore__DomainService__AppId", apps[project.Resource]);
                foreach (var wait in project.Resource.Annotations.OfType<WaitAnnotation>().Where(wait => wait.WaitType == WaitType.WaitUntilHealthy
                    && receivers.Any(peer => ReferenceEquals(peer.Project.Resource, wait.Resource))).ToArray())
                {
                    project.Resource.Annotations.Remove(wait);
                    project.WaitForStart(builder.CreateResourceBuilder(wait.Resource));
                }
            }
            for (int i = 0; i < owners.Count; i++)
            {
                gateway.WithEnvironment("EventStore__RouteCatalog__DevelopmentBootstrap__OwnerAssemblies__" + i, owners[i].Contracts.Location);
                gateway.WithEnvironment("EventStore__RouteCatalog__DevelopmentBootstrap__AdapterAssemblies__" + i, owners[i].Adapters.Location);
            }
            gateway.WithEnvironment("EventStore__IdempotencyAdmission__Enabled", "true")
                .WithEnvironment("EventStore__IdempotencyAdmission__DigestKeySource", "Configuration")
                .WithEnvironment("EventStore__IdempotencyAdmission__ActiveDigestKeyVersion", "development-v1")
                .WithEnvironment("EventStore__IdempotencyAdmission__DigestKeys__development-v1", key)
                .WithEnvironment("EventStore__IdempotencyAdmission__DigestKeySecretStoreName", "secretstore")
                .WithEnvironment("EventStore__IdempotencyAdmission__DigestKeySecretName", "eventstore-idempotency-ring")
                .WithEnvironment("EventStore__IdempotencyAdmission__DigestKeySecretGeneration", "development-v1");
            return new(directory, root);
        }
        catch { Directory.Delete(directory, recursive: true); throw; }
    }

    private static RouteCatalogEnvelope BuildCatalog(string gatewayAppId, IReadOnlyList<DevelopmentRouteCatalogOwner> owners, IReadOnlyDictionary<ProjectResource, string> apps)
    {
        List<RouteContractDeclaration> declarations = []; List<RouteOperationContract> operations = [];
        List<RouteCompositionBinding> bindings = []; List<IdempotencyCatalogEntry> policies = [];
        foreach (var owner in owners)
        {
            var routes = CompiledRouteCatalogDeclarations.Read([owner.Contracts]);
            declarations.AddRange(routes); operations.AddRange(CompiledRouteCatalogDeclarations.ReadOperations([owner.Contracts]));
            foreach (var source in owner.Contracts.GetCustomAttributes<RouteCatalogDeclarationSourceAttribute>())
            {
                if (source.SourceType.GetProperty("ReceiverMethods", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) is not IReadOnlyDictionary<string, string> methods
                    || source.SourceType.GetProperty("Commands", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) is not IEnumerable<CommandIntentDeclaration> commands)
                    throw new InvalidOperationException("The complete compiled owner receiver and command-policy inventories are required.");
                foreach (var route in routes.Where(route => methods.ContainsKey(route.RouteId)))
                {
                    bool actorQuery = RouteCatalogProjectionActorPolicy.IsQuery(route);
                    if (actorQuery && methods[route.RouteId] != RouteCatalogProjectionActorPolicy.Method)
                        throw new InvalidOperationException("The owner actor query differs from its existing SDK method contract.");
                    bindings.Add(new(route.RouteId, actorQuery ? gatewayAppId : apps[owner.Project.Resource], methods[route.RouteId], route.ContractVersion, false, "*"));
                }
                policies.AddRange(commands.Select(command => DevelopmentIdempotencyBindings.Bind(command, 1, gatewayAppId)));
            }
        }
        if (bindings.Count != declarations.Count || declarations.Any(declaration => declaration.Execution?.TargetKind != "domain-service" && !RouteCatalogProjectionActorPolicy.IsQuery(declaration)))
            throw new InvalidOperationException("Every selected domain-service declaration requires one compiled receiver binding.");
        return VersionedRouteCatalogBuilder.Build(new(1, "Development", declarations, operations, bindings, policies));
    }

    private static IDaprSidecarResource Sidecar(ProjectResource project) => project.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
    private static string AppId(ProjectResource project) => Sidecar(project).Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options.AppId
        ?? throw new InvalidOperationException("A selected sidecar lacks its actual AppId.");
    internal static JsonObject ReadYamlOrJson(string path)
    {
        string bytes = File.ReadAllText(Path.GetFullPath(path));
        object document = new DeserializerBuilder().WithAttemptingUnquotedStringTypeDeserialization().Build().Deserialize<object>(bytes);
        return JsonNode.Parse(new SerializerBuilder().JsonCompatible().Build().Serialize(document))!.AsObject();
    }
    private static string Write(string directory, string name, byte[] bytes)
    {
        string path = Path.Combine(directory, name);
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using var stream = new FileStream(path, options); stream.Write(bytes); return path;
    }
}
