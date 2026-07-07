namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using global::Aspire.Hosting;
using global::Aspire.Hosting.ApplicationModel;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.Aspire;

public class DomainModuleAspireExtensionTests {
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
        GetReferencedComponentNames(sidecar).ShouldBe(["pubsub", "statestore"]);
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
        options.ResourcesPaths.ShouldContain(IsolatedResourcesPath);
        GetReferencedComponentNames(sidecar).ShouldBe([]);
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
}
