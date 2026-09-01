namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using global::Aspire.Hosting;
using global::Aspire.Hosting.ApplicationModel;
using Hexalith.EventStore.Aspire;

public class HexalithEventStoreSecurityExtensionsTests {
    private const string SecurityResourceName = "security";

    [Fact]
    public void AddHexalithEventStoreSecurity_WhenDefault_UsesProxylessDynamicKeycloakEndpoints() {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.Configuration[HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey] = "true";
        builder.Configuration[HexalithEventStoreSecurityOptions.DefaultPersistentConfigurationKey] = "false";
        builder.Configuration[HexalithEventStoreSecurityOptions.DefaultHttpPortConfigurationKey] = "not-a-port";
        builder.Configuration[HexalithEventStoreSecurityOptions.DefaultManagementPortConfigurationKey] = "not-a-port";

        HexalithEventStoreSecurityResources security = builder.AddHexalithEventStoreSecurity()!;

        HexalithEventStoreSecurityOptions.DefaultResourceName.ShouldBe(SecurityResourceName);
        security.Keycloak.Resource.Name.ShouldBe(SecurityResourceName);
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
    public void AddHexalithEventStoreSecurity_WhenEnableKeycloakUnset_CreatesTheSecurityResource() {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        // Clear rather than set: the other tests pin the switch to "true" for hermeticity, which
        // would also pass if the resource ever became opt-in. This is the genuinely default case.
        builder.Configuration[HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey] = null;

        HexalithEventStoreSecurityResources? security = builder.AddHexalithEventStoreSecurity();

        security.ShouldNotBeNull();
        security.Keycloak.Resource.Name.ShouldBe(SecurityResourceName);
    }

    [Fact]
    public void AddHexalithEventStoreSecurity_WhenDisabled_CreatesNoSecurityResource() {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.Configuration[HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey] = "false";

        HexalithEventStoreSecurityResources? security = builder.AddHexalithEventStoreSecurity();

        security.ShouldBeNull();
        builder.Resources.ShouldNotContain(
            static resource => string.Equals(resource.Name, SecurityResourceName, StringComparison.Ordinal));
    }

    [Fact]
    public void AddHexalithEventStoreSecurity_WhenResourceNameOverridden_UsesTheOverride() {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.Configuration[HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey] = "true";

        HexalithEventStoreSecurityResources security = builder.AddHexalithEventStoreSecurity(
            new HexalithEventStoreSecurityOptions { ResourceName = "identity" })!;

        security.Keycloak.Resource.Name.ShouldBe("identity");
        HexalithEventStoreSecurityOptions.DefaultResourceName.ShouldBe(SecurityResourceName);
    }

    [Fact]
    public async Task AddHexalithEventStoreSecurity_WhenRealmOptionsDefault_PreservesRealmUrlAndImportAnnotation()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.Configuration[HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey] = "true";
        builder.Configuration[HexalithEventStoreSecurityOptions.DefaultPersistentConfigurationKey] = "false";

        HexalithEventStoreSecurityResources security = builder.AddHexalithEventStoreSecurity()!;

        security.Keycloak.Resource.Name.ShouldBe(SecurityResourceName);
        AssertRealmUrl(security, HexalithEventStoreSecurityOptions.DefaultRealmName);
        await AssertRealmImportAsync(
            builder,
            security,
            HexalithEventStoreSecurityOptions.DefaultRealmImportPath).ConfigureAwait(true);
    }

    [Fact]
    public async Task AddHexalithEventStoreSecurity_WhenRealmOptionsOverridden_PreservesRealmUrlAndImportAnnotation()
    {
        string realmImportPath = Directory.CreateTempSubdirectory("eventstore-realm-import-").FullName;
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(realmImportPath, "override-realm.json"),
                "{}",
                CancellationToken.None).ConfigureAwait(true);
            IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
            builder.Configuration[HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey] = "true";
            builder.Configuration[HexalithEventStoreSecurityOptions.DefaultPersistentConfigurationKey] = "false";
            var options = new HexalithEventStoreSecurityOptions
            {
                RealmName = "review-realm",
                RealmImportPath = realmImportPath,
            };

            HexalithEventStoreSecurityResources security = builder.AddHexalithEventStoreSecurity(options)!;

            security.Keycloak.Resource.Name.ShouldBe(SecurityResourceName);
            AssertRealmUrl(security, options.RealmName);
            await AssertRealmImportAsync(
                builder,
                security,
                realmImportPath,
                "override-realm.json").ConfigureAwait(true);
        }
        finally
        {
            Directory.Delete(realmImportPath, recursive: true);
        }
    }

    [Fact]
    public void WithSecurityDependency_WhenConfigured_AddsReferenceAndWaitEdgesToSecurity() {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.Configuration[HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey] = "true";
        HexalithEventStoreSecurityResources security = builder.AddHexalithEventStoreSecurity()!;
        IResourceBuilder<ProjectResource> dependent = builder.AddProject<EventStoreProjectMetadata>("dependent");

        _ = dependent.WithSecurityDependency(security);

        dependent.Resource.Annotations
            .OfType<ResourceRelationshipAnnotation>()
            .Where(static annotation => string.Equals(annotation.Type, "Reference", StringComparison.Ordinal))
            .Select(static annotation => annotation.Resource.Name)
            .ShouldBe([SecurityResourceName]);
        dependent.Resource.Annotations
            .OfType<WaitAnnotation>()
            .Select(static annotation => annotation.Resource.Name)
            .ShouldBe([SecurityResourceName]);
    }

    [Fact]
    public void AddHexalithEventStoreSecurity_WhenPersistent_UsesProxylessFixedKeycloakEndpoints() {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.Configuration[HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey] = "true";
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
    public void WithEventStoreAuthenticationValidation_ForwardsValidationSettingsWithoutServiceCredentials() {
        string source = File.ReadAllText(Path.Combine(
            RepositoryProjectPaths.GetRepositoryRoot(),
            "src",
            "Hexalith.EventStore.Aspire",
            "HexalithEventStoreSecurityExtensions.cs"));

        string method = ExtractMethod(source, "public static IResourceBuilder<ProjectResource> WithEventStoreAuthenticationValidation");

        method.ShouldContain(".WithEnvironment(\"EventStore__Authentication__Authority\", security.RealmUrl)");
        method.ShouldContain(".WithEnvironment(\"EventStore__Authentication__Audience\", security.Audience)");
        method.ShouldContain(".WithEnvironment(\"EventStore__Authentication__RequireHttpsMetadata\", ToConfigurationValue(security.RequireHttpsMetadata))");
        method.ShouldNotContain("EventStore__Authentication__ClientId");
        method.ShouldNotContain("EventStore__Authentication__Username");
        method.ShouldNotContain("EventStore__Authentication__Password");
    }

    [Fact]
    public void WithEventStoreClientCredentials_ComposesValidationAndAddsServiceAccountSettings() {
        string source = File.ReadAllText(Path.Combine(
            RepositoryProjectPaths.GetRepositoryRoot(),
            "src",
            "Hexalith.EventStore.Aspire",
            "HexalithEventStoreSecurityExtensions.cs"));

        string method = ExtractMethod(source, "public static IResourceBuilder<ProjectResource> WithEventStoreClientCredentials");

        method.ShouldContain(".WithEventStoreAuthenticationValidation(security)");
        method.ShouldContain(".WithEnvironment(\"EventStore__Authentication__ClientId\", clientId)");
        method.ShouldContain(".WithEnvironment(\"EventStore__Authentication__Username\", username)");
        method.ShouldContain(".WithEnvironment(\"EventStore__Authentication__Password\", password)");
    }

    private static EndpointAnnotation GetEndpoint(HexalithEventStoreSecurityResources security, string name) {
        return security.Keycloak.Resource.Annotations
            .OfType<EndpointAnnotation>()
            .Single(e => e.Name == name);
    }

    private static async Task AssertRealmImportAsync(
        IDistributedApplicationBuilder builder,
        HexalithEventStoreSecurityResources security,
        string expectedImportPath,
        string? expectedImportedFileName = null)
    {
        ContainerFileSystemCallbackAnnotation annotation = security.Keycloak.Resource.Annotations
            .OfType<ContainerFileSystemCallbackAnnotation>()
            .Single();
        annotation.DestinationPath.ShouldBe("/opt/keycloak/data/import");

        IEnumerable<ContainerFileSystemItem> importedItems = await annotation.Callback(
            new ContainerFileSystemCallbackContext
            {
                Model = security.Keycloak.Resource,
                ServiceProvider = null!,
                Services = null!,
            },
            CancellationToken.None).ConfigureAwait(true);
        string expectedFullPath = Path.GetFullPath(expectedImportPath, builder.AppHostDirectory);
        string expectedPrefix = expectedFullPath.EndsWith(Path.DirectorySeparatorChar)
            ? expectedFullPath
            : expectedFullPath + Path.DirectorySeparatorChar;
        string[] sourcePaths =
        [
            .. importedItems
                .OfType<ContainerFileBase>()
                .Select(static item => item.SourcePath)
                .Where(static path => path is not null)
                .Select(static path => path!),
        ];
        sourcePaths.ShouldNotBeEmpty();
        sourcePaths.ShouldAllBe(
            path => path.StartsWith(expectedPrefix, StringComparison.Ordinal),
            $"Expected every imported realm file to originate under {expectedFullPath}.");
        if (expectedImportedFileName is not null)
        {
            sourcePaths.ShouldContain(Path.Combine(expectedFullPath, expectedImportedFileName));
        }
    }

    private static void AssertRealmUrl(HexalithEventStoreSecurityResources security, string expectedRealmName)
    {
        ReferenceExpression expectedRealmUrl = ReferenceExpression.Create(
            $"{security.Keycloak.GetEndpoint("http")}/realms/{expectedRealmName}");
        security.RealmUrl.ValueExpression.ShouldBe(expectedRealmUrl.ValueExpression);
        EndpointReference realmEndpoint = security.RealmUrl.ValueProviders
            .OfType<EndpointReference>()
            .Single();
        realmEndpoint.Resource.ShouldBeSameAs(security.Keycloak.Resource);
        realmEndpoint.EndpointName.ShouldBe("http");
    }

    private static string ExtractMethod(string source, string marker) {
        int start = source.IndexOf(marker, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"Expected source to contain {marker}.");
        int end = source.IndexOf("    /// <summary>", start + marker.Length, StringComparison.Ordinal);
        end.ShouldBeGreaterThan(start, "Expected the next XML documentation block after the method.");
        return source[start..end];
    }
}
