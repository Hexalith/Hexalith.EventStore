namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using global::Aspire.Hosting;
using global::Aspire.Hosting.ApplicationModel;
using Hexalith.EventStore.Aspire;

public class HexalithEventStoreSecurityExtensionsTests {
    [Fact]
    public void AddHexalithEventStoreSecurity_WhenDefault_UsesProxylessDynamicKeycloakEndpoints() {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.Configuration[HexalithEventStoreSecurityOptions.DefaultPersistentConfigurationKey] = "false";
        builder.Configuration[HexalithEventStoreSecurityOptions.DefaultHttpPortConfigurationKey] = "not-a-port";
        builder.Configuration[HexalithEventStoreSecurityOptions.DefaultManagementPortConfigurationKey] = "not-a-port";

        HexalithEventStoreSecurityResources security = builder.AddHexalithEventStoreSecurity()!;

        EndpointAnnotation http = GetEndpoint(security, "http");
        EndpointAnnotation management = GetEndpoint(security, "management");
        http.Port.ShouldNotBeNull();
        http.Port.Value.ShouldBeGreaterThan(0);
        http.Port.Value.ShouldNotBe(KeycloakFastStartPorts.ReservedEventStoreAppPort);
        http.TargetPort.ShouldBe(8080);
        http.IsExplicitlyProxied.ShouldBe(false);
        management.Port.ShouldNotBeNull();
        management.Port.Value.ShouldBeGreaterThan(0);
        management.Port.ShouldNotBe(http.Port);
        management.TargetPort.ShouldBe(9000);
        management.IsExplicitlyProxied.ShouldBe(false);
    }

    [Fact]
    public void AddHexalithEventStoreSecurity_WhenPersistent_UsesProxylessFixedKeycloakEndpoints() {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.Configuration[HexalithEventStoreSecurityOptions.DefaultPersistentConfigurationKey] = "true";
        builder.Configuration[HexalithEventStoreSecurityOptions.DefaultHttpPortConfigurationKey] = "9180";
        builder.Configuration[HexalithEventStoreSecurityOptions.DefaultManagementPortConfigurationKey] = "9543";

        HexalithEventStoreSecurityResources security = builder.AddHexalithEventStoreSecurity()!;

        EndpointAnnotation http = GetEndpoint(security, "http");
        EndpointAnnotation management = GetEndpoint(security, "management");
        http.Port.ShouldBe(9180);
        http.TargetPort.ShouldBe(8080);
        http.IsExplicitlyProxied.ShouldBe(false);
        management.Port.ShouldBe(9543);
        management.TargetPort.ShouldBe(9000);
        management.IsExplicitlyProxied.ShouldBe(false);
    }

    [Fact]
    public void WithEventStoreClientCredentials_ForwardsAuthenticationValidationSettings() {
        string source = File.ReadAllText(Path.Combine(
            RepositoryProjectPaths.GetRepositoryRoot(),
            "src",
            "Hexalith.EventStore.Aspire",
            "HexalithEventStoreSecurityExtensions.cs"));

        string method = ExtractMethod(source, "public static IResourceBuilder<ProjectResource> WithEventStoreClientCredentials");

        method.ShouldContain(".WithEnvironment(\"EventStore__Authentication__Authority\", security.RealmUrl)");
        method.ShouldContain(".WithEnvironment(\"EventStore__Authentication__Audience\", security.Audience)");
        method.ShouldContain(".WithEnvironment(\"EventStore__Authentication__RequireHttpsMetadata\", ToConfigurationValue(security.RequireHttpsMetadata))");
        method.ShouldContain(".WithEnvironment(\"EventStore__Authentication__ClientId\", clientId)");
    }

    private static EndpointAnnotation GetEndpoint(HexalithEventStoreSecurityResources security, string name) {
        return security.Keycloak.Resource.Annotations
            .OfType<EndpointAnnotation>()
            .Single(e => e.Name == name);
    }

    private static string ExtractMethod(string source, string marker) {
        int start = source.IndexOf(marker, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"Expected source to contain {marker}.");
        int end = source.IndexOf("    /// <summary>", start + marker.Length, StringComparison.Ordinal);
        end.ShouldBeGreaterThan(start, "Expected the next XML documentation block after the method.");
        return source[start..end];
    }
}
