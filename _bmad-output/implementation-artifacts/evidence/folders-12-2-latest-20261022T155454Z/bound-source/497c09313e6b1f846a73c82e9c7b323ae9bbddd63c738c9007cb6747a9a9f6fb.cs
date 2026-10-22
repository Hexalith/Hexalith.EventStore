namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using global::Aspire.Hosting;
using global::Aspire.Hosting.ApplicationModel;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.Aspire;

public class DomainModuleAspireExtensionTests {
    [Fact]
    public async Task EventStoreTopologyOwnsTokenSharedOnlyWithItsSidecarAsync()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        IResourceBuilder<ProjectResource> gateway = builder.AddProject<EventStoreProjectMetadata>("eventstore");
        IResourceBuilder<ProjectResource> admin = builder.AddProject<EventStoreProjectMetadata>("admin");
        builder.AddHexalithEventStore(gateway, admin);
        ParameterResource token = builder.Resources.OfType<ParameterResource>()
            .Single(resource => resource.Name == "eventstore-app-api-token");
        token.Secret.ShouldBeTrue();

        Dictionary<string, object> appEnvironment = [];
        foreach (EnvironmentCallbackAnnotation callback in gateway.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, gateway.Resource, appEnvironment, TestContext.Current.CancellationToken));
        appEnvironment[HexalithEventStoreAppChannelExtensions.AppChannelTokenVariable].ShouldBeSameAs(token);

        IDaprSidecarResource sidecar = GetSidecar(gateway);
        Dictionary<string, object> sidecarEnvironment = [];
        foreach (EnvironmentCallbackAnnotation callback in sidecar.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, sidecar, sidecarEnvironment, TestContext.Current.CancellationToken));
        sidecarEnvironment[HexalithEventStoreAppChannelExtensions.AppChannelTokenVariable].ShouldBeSameAs(token);

        Dictionary<string, object> adminEnvironment = [];
        foreach (EnvironmentCallbackAnnotation callback in admin.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, admin.Resource, adminEnvironment, TestContext.Current.CancellationToken));
        adminEnvironment.ShouldNotContainKey(HexalithEventStoreAppChannelExtensions.AppChannelTokenVariable);
    }

    [Fact]
    public async Task AssignedGatewayComponentNamesReachResourcesOwnerOptionsAndSidecarAsync()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        IResourceBuilder<ProjectResource> gateway = builder.AddProject<EventStoreProjectMetadata>("eventstore");
        IResourceBuilder<ProjectResource> admin = builder.AddProject<EventStoreProjectMetadata>("admin");
        HexalithEventStoreResources resources = builder.AddHexalithEventStore(gateway, admin, null, null, null, null, null,
            3501, null, null, null, "assigned-state", "assigned-events");
        resources.StateStore.Resource.Name.ShouldBe("assigned-state");
        resources.PubSub.Resource.Name.ShouldBe("assigned-events");
        GetReferencedComponentNames(GetSidecar(gateway)).ShouldBe(["assigned-events", "assigned-state"]);
        Dictionary<string, object> environment = [];
        foreach (EnvironmentCallbackAnnotation callback in gateway.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, gateway.Resource, environment, TestContext.Current.CancellationToken));
        }

        environment["EventStore__CommandStatus__StateStoreName"].ShouldBe("assigned-state");
        environment["EventStore__Projections__ReadModelStateStoreName"].ShouldBe("assigned-state");
        environment["EventStore__Publisher__PubSubName"].ShouldBe("assigned-events");
        environment.Clear();
        foreach (EnvironmentCallbackAnnotation callback in admin.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, admin.Resource, environment, TestContext.Current.CancellationToken));
        environment["AdminServer__StateStoreName"].ShouldBe("assigned-state");
        GetReferencedComponentNames(GetSidecar(admin)).ShouldBe(["assigned-state"]);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void SuppliedComponentFilesMustMatchAssignedNamesBeforeResourcesAreAdded(bool wrongState, bool wrongPubSub)
    {
        string directory = Path.Combine(Path.GetTempPath(), "assigned-components-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string state = Path.Combine(directory, "state.yaml");
            string events = Path.Combine(directory, "events.yaml");
            File.WriteAllText(state, "apiVersion: dapr.io/v1alpha1\nkind: Component\nmetadata:\n  name: " + (wrongState ? "statestore" : "assigned-state") + "\nspec:\n  type: state.redis\n  version: v1\n  metadata:\n  - name: redisHost\n    value: localhost:6379\n");
            File.WriteAllText(events, "apiVersion: dapr.io/v1alpha1\nkind: Component\nmetadata:\n  name: " + (wrongPubSub ? "pubsub" : "assigned-events") + "\nspec:\n  type: pubsub.redis\n  version: v1\n  metadata:\n  - name: redisHost\n    value: localhost:6379\n");
            IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
            IResourceBuilder<ProjectResource> gateway = builder.AddProject<EventStoreProjectMetadata>("eventstore");
            IResource[] existing = builder.Resources.ToArray();
            if (wrongState || wrongPubSub)
            {
                InvalidDataException failure = Should.Throw<InvalidDataException>(() => builder.AddHexalithEventStore(gateway, null, null, null, null, null, state, 3501, null, null, events, "assigned-state", "assigned-events"));
                failure.Message.ShouldContain(wrongState ? state : events);
                failure.Message.ShouldContain("metadata.name");
                builder.Resources.ShouldBe(existing);
                gateway.Resource.Annotations.OfType<DaprSidecarAnnotation>().ShouldBeEmpty();
            }
            else
            {
                HexalithEventStoreResources resources = builder.AddHexalithEventStore(gateway, null, null, null, null, null, state, 3501, null, null, events, "assigned-state", "assigned-events");
                resources.StateStore.Resource.Name.ShouldBe("assigned-state");
                resources.PubSub.Resource.Name.ShouldBe("assigned-events");
                GetReferencedComponentNames(GetSidecar(gateway)).ShouldBe(["assigned-events", "assigned-state"]);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void AddEventStoreDomainModule_WhenShared_EnablesAliveAppHealthAndReferencesSharedComponents() {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        HexalithEventStoreResources eventStoreResources = CreateEventStoreResources(builder);
        IResourceBuilder<ProjectResource> domainModule = builder.AddProject<EventStoreProjectMetadata>("shared-domain");

        _ = domainModule.AddEventStoreDomainModule(eventStoreResources, "tenants");

        IDaprSidecarResource sidecar = GetSidecar(domainModule);
        DaprSidecarOptions options = GetOptions(sidecar);
        options.EnableAppHealthCheck.ShouldBe(true);
        options.AppHealthCheckPath.ShouldBe("/alive");
        options.AppPort.ShouldBeNull();
        GetReferencedComponentNames(sidecar).ShouldBe(["pubsub", "statestore"]);
        GetReferencedResourceNames(domainModule).ShouldContain("eventstore");
        GetWaitedResourceNames(domainModule).ShouldContain("eventstore");
    }

    [Fact]
    public void AddEventStoreDomainModule_WhenIsolated_EnablesAliveAppHealthWithoutSharedComponents() {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        HexalithEventStoreResources eventStoreResources = CreateEventStoreResources(builder);
        IResourceBuilder<ProjectResource> domainModule = builder.AddProject<EventStoreProjectMetadata>("isolated-domain");
        const string IsolatedResourcesPath = "/tmp/hexalith-empty-dapr";

        _ = domainModule.AddEventStoreDomainModule(
            eventStoreResources,
            "sample",
            isolatedDaprResourcesPath: IsolatedResourcesPath);

        IDaprSidecarResource sidecar = GetSidecar(domainModule);
        DaprSidecarOptions options = GetOptions(sidecar);
        options.EnableAppHealthCheck.ShouldBe(true);
        options.AppHealthCheckPath.ShouldBe("/alive");
        options.AppPort.ShouldBeNull();
        options.ResourcesPaths.ShouldContain(IsolatedResourcesPath);
        GetReferencedComponentNames(sidecar).ShouldBe([]);
        GetReferencedResourceNames(domainModule).ShouldContain("eventstore");
        GetWaitedResourceNames(domainModule).ShouldContain("eventstore");
    }

    [Fact]
    public void AddEventStoreDomainModule_WhenAppPortIsExplicit_PassesItToSidecar() {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        HexalithEventStoreResources eventStoreResources = CreateEventStoreResources(builder);
        IResourceBuilder<ProjectResource> domainModule = builder.AddProject<EventStoreProjectMetadata>("multi-listener-domain");

        _ = domainModule.AddEventStoreDomainModule(eventStoreResources, "timesheets", appPort: 8081);

        GetOptions(GetSidecar(domainModule)).AppPort.ShouldBe(8081);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void AddEventStoreDomainModule_WhenAppPortIsInvalid_RejectsIt(int appPort) {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        HexalithEventStoreResources eventStoreResources = CreateEventStoreResources(builder);
        IResourceBuilder<ProjectResource> domainModule = builder.AddProject<EventStoreProjectMetadata>("invalid-port-domain");

        Should.Throw<ArgumentOutOfRangeException>(() => domainModule.AddEventStoreDomainModule(
            eventStoreResources,
            "invalid-port-domain",
            appPort: appPort));
    }

    [Theory]
    [InlineData("/ready")]
    [InlineData("/custom-health")]
    public void AddAspireDaprDomainModule_WhenAppHealthPathIsExplicit_PreservesPath(string appHealthPath) {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        HexalithEventStoreResources eventStoreResources = CreateEventStoreResources(builder);
        IResourceBuilder<ProjectResource> domainModule = builder.AddProject<EventStoreProjectMetadata>("explicit-domain");

        _ = domainModule.AddAspireDaprDomainModule(new AspireDaprDomainModuleOptions(
            "explicit-domain",
            AspireDaprInfrastructureMode.Shared) {
            SharedComponents = new AspireDaprSharedComponents(eventStoreResources.StateStore, eventStoreResources.PubSub),
            EnableAppHealthCheck = true,
            AppHealthCheckPath = appHealthPath,
        });

        IDaprSidecarResource sidecar = GetSidecar(domainModule);
        DaprSidecarOptions options = GetOptions(sidecar);
        options.EnableAppHealthCheck.ShouldBe(true);
        options.AppHealthCheckPath.ShouldBe(appHealthPath);
        GetReferencedComponentNames(sidecar).ShouldBe(["pubsub", "statestore"]);
    }

    private static HexalithEventStoreResources CreateEventStoreResources(IDistributedApplicationBuilder builder) {
        IResourceBuilder<IDaprComponentResource> stateStore = builder.AddDaprComponent("statestore", "state.redis");
        IResourceBuilder<IDaprComponentResource> pubSub = builder.AddDaprPubSub("pubsub");
        IResourceBuilder<ProjectResource> eventStore = builder.AddProject<EventStoreProjectMetadata>("eventstore");
        return new HexalithEventStoreResources(stateStore, pubSub, eventStore, null, null);
    }

    private static IDaprSidecarResource GetSidecar(IResourceBuilder<ProjectResource> project) {
        project.Resource.TryGetAnnotationsOfType<DaprSidecarAnnotation>(out IEnumerable<DaprSidecarAnnotation>? annotations)
            .ShouldBeTrue();
        DaprSidecarAnnotation[] sidecarAnnotations = annotations!.ToArray();
        sidecarAnnotations.Length.ShouldBe(1);
        return sidecarAnnotations[0].Sidecar;
    }

    private static DaprSidecarOptions GetOptions(IDaprSidecarResource sidecar) {
        sidecar.TryGetLastAnnotation<DaprSidecarOptionsAnnotation>(out DaprSidecarOptionsAnnotation? annotation).ShouldBeTrue();
        return annotation!.Options;
    }

    private static string[] GetReferencedComponentNames(IDaprSidecarResource sidecar) {
        return sidecar.TryGetAnnotationsOfType<DaprComponentReferenceAnnotation>(out IEnumerable<DaprComponentReferenceAnnotation>? annotations)
            ? annotations
                .Select(static annotation => annotation.Component.Name)
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToArray()
            : [];
    }

    private static string[] GetReferencedResourceNames(IResourceBuilder<ProjectResource> project)
        => [.. project.Resource.Annotations
            .OfType<ResourceRelationshipAnnotation>()
            .Select(static annotation => annotation.Resource.Name)
            .Order(StringComparer.Ordinal)];

    private static string[] GetWaitedResourceNames(IResourceBuilder<ProjectResource> project)
        => [.. project.Resource.Annotations
            .OfType<WaitAnnotation>()
            .Select(static annotation => annotation.Resource.Name)
            .Order(StringComparer.Ordinal)];
}
