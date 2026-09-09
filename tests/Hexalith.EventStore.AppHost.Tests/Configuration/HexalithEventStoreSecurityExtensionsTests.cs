namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using global::Aspire.Hosting;
using global::Aspire.Hosting.ApplicationModel;
using Hexalith.EventStore.Aspire;
using System.Text.Json;

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

        const string marker = "public static IResourceBuilder<ProjectResource> WithEventStoreClientCredentials";
        int firstOverload = source.IndexOf(marker, StringComparison.Ordinal);
        firstOverload.ShouldBeGreaterThanOrEqualTo(0);
        string method = ExtractMethod(source[(firstOverload + marker.Length)..], marker);

        method.ShouldContain(".WithEventStoreAuthenticationValidation(security)");
        method.ShouldContain(".WithEnvironment(\"EventStore__Authentication__ClientId\", clientId)");
        method.ShouldContain(".WithEnvironment(\"EventStore__Authentication__Username\", username)");
        method.ShouldContain(".WithEnvironment(\"EventStore__Authentication__Password\", password)");
    }

    [Fact]
    public async Task WithEventStoreClientCredentials_OneArgumentOverloadBindsTheProvisionedRealmIdentity()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.Configuration[HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey] = "true";
        builder.Configuration[HexalithEventStoreSecurityOptions.DefaultPersistentConfigurationKey] = "false";
        HexalithEventStoreSecurityResources security = builder.AddHexalithEventStoreSecurity()!;
        IResourceBuilder<ProjectResource> resource = builder.AddProject<EventStoreProjectMetadata>("consumer");

        _ = resource.WithEventStoreClientCredentials(security);

        ParameterResource usernameParameter = security.DefaultClientUsername!.Resource;
        ParameterResource passwordParameter = security.DefaultClientPassword!.Resource;
        builder.Resources.ShouldContain(usernameParameter);
        builder.Resources.ShouldContain(passwordParameter);
        usernameParameter.Secret.ShouldBeTrue();
        passwordParameter.Secret.ShouldBeTrue();

        IReadOnlyDictionary<string, object> consumerEnvironment = await GetEnvironmentAsync(
            resource.Resource,
            builder.ExecutionContext).ConfigureAwait(true);
        IReadOnlyDictionary<string, object> keycloakEnvironment = await GetEnvironmentAsync(
            security.Keycloak.Resource,
            builder.ExecutionContext).ConfigureAwait(true);
        string consumerUsername = await ResolveAsync(
            consumerEnvironment["EventStore__Authentication__Username"],
            resource.Resource,
            builder.ExecutionContext).ConfigureAwait(true);
        string consumerPassword = await ResolveAsync(
            consumerEnvironment["EventStore__Authentication__Password"],
            resource.Resource,
            builder.ExecutionContext).ConfigureAwait(true);
        string realmUsername = await ResolveAsync(
            keycloakEnvironment[HexalithEventStoreSecurityOptions.DefaultClientUsernameEnvironmentName],
            security.Keycloak.Resource,
            builder.ExecutionContext).ConfigureAwait(true);
        string realmPassword = await ResolveAsync(
            keycloakEnvironment[HexalithEventStoreSecurityOptions.DefaultClientPasswordEnvironmentName],
            security.Keycloak.Resource,
            builder.ExecutionContext).ConfigureAwait(true);
        consumerUsername.Length.ShouldBeGreaterThanOrEqualTo(24);
        consumerPassword.ShouldNotBeNullOrWhiteSpace();
        string.Equals(consumerUsername, realmUsername, StringComparison.Ordinal).ShouldBeTrue();
        string.Equals(consumerPassword, realmPassword, StringComparison.Ordinal).ShouldBeTrue();

        string realmPath = Path.Combine(
            RepositoryProjectPaths.GetRepositoryRoot(),
            "references",
            "Hexalith.Tenants",
            "src",
            "Hexalith.Tenants.AppHost",
            "KeycloakRealms",
            "hexalith-realm.json");
        string realmTemplate = File.ReadAllText(realmPath);
        using JsonDocument template = JsonDocument.Parse(realmTemplate);
        JsonElement templateUser = template.RootElement.GetProperty("users").EnumerateArray().Single(
            static user => user.GetProperty("username").GetString()
                == "${HEXALITH_EVENTSTORE_CLIENT_USERNAME}");
        templateUser.GetProperty("credentials")[0].GetProperty("value").GetString().ShouldBe(
            "${HEXALITH_EVENTSTORE_CLIENT_PASSWORD}");

        string renderedRealm = realmTemplate
            .Replace(
                "\"${HEXALITH_EVENTSTORE_CLIENT_USERNAME}\"",
                JsonSerializer.Serialize(realmUsername),
                StringComparison.Ordinal)
            .Replace(
                "\"${HEXALITH_EVENTSTORE_CLIENT_PASSWORD}\"",
                JsonSerializer.Serialize(realmPassword),
                StringComparison.Ordinal);
        using JsonDocument realm = JsonDocument.Parse(renderedRealm);
        JsonElement provisionedUser = realm.RootElement.GetProperty("users").EnumerateArray().Single(
            user => string.Equals(
                user.GetProperty("username").GetString(),
                consumerUsername,
                StringComparison.Ordinal));
        provisionedUser.GetProperty("enabled").GetBoolean().ShouldBeTrue();
        provisionedUser.GetProperty("credentials")[0].GetProperty("type").GetString().ShouldBe("password");
        string.Equals(
            provisionedUser.GetProperty("credentials")[0].GetProperty("value").GetString(),
            consumerPassword,
            StringComparison.Ordinal).ShouldBeTrue();
        provisionedUser.GetProperty("credentials")[0].GetProperty("temporary").GetBoolean().ShouldBeFalse();
        JsonElement directGrantClient = realm.RootElement.GetProperty("clients").EnumerateArray().Single(
            static client => client.GetProperty("clientId").GetString() == "hexalith-eventstore");
        directGrantClient.GetProperty("enabled").GetBoolean().ShouldBeTrue();
        directGrantClient.GetProperty("publicClient").GetBoolean().ShouldBeTrue();
        directGrantClient.GetProperty("directAccessGrantsEnabled").GetBoolean().ShouldBeTrue();
    }

    [Theory]
    [InlineData("tokens.example.test/oauth/token")]
    [InlineData("http://tokens.example.test/oauth/token")]
    [InlineData("https://user@tokens.example.test/oauth/token")]
    [InlineData("https://tokens.example.test/oauth/token?tenant=x")]
    [InlineData("https://tokens.example.test/oauth/token#tenant")]
    public void WithExternalEventStoreClientCredentials_InvalidEndpointFailsBeforeResourceMutation(string endpoint)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        IResourceBuilder<ProjectResource> resource = builder.AddProject<EventStoreProjectMetadata>("consumer");
        IResourceBuilder<ParameterResource> clientId = builder.AddParameter("client-id", static () => "client");
        IResourceBuilder<ParameterResource> clientSecret = builder.AddParameter(
            "client-secret",
            static () => "<runtime-generated>",
            secret: true);
        int annotationsBefore = resource.Resource.Annotations.Count;

        _ = Should.Throw<ArgumentException>(() => resource.WithExternalEventStoreClientCredentials(
            "https://identity.example.test/tenant",
            "eventstore-api",
            endpoint,
            "api.read",
            "client_credentials",
            clientId,
            username: null,
            password: null,
            clientSecret,
            audienceParameterName: null,
            audienceParameterValue: null));

        resource.Resource.Annotations.Count.ShouldBe(annotationsBefore);
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

    private static async Task<IReadOnlyDictionary<string, object>> GetEnvironmentAsync(
        IResource resource,
        DistributedApplicationExecutionContext executionContext)
    {
        var context = new EnvironmentCallbackContext(
            executionContext,
            resource,
            new Dictionary<string, object>(),
            CancellationToken.None);
        foreach (EnvironmentCallbackAnnotation annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context).ConfigureAwait(true);
        }

        return context.EnvironmentVariables;
    }

    private static async Task<string> ResolveAsync(
        object value,
        IResource caller,
        DistributedApplicationExecutionContext executionContext)
    {
        if (value is not IValueProvider provider)
        {
            return value.ToString() ?? string.Empty;
        }

        return await provider.GetValueAsync(
            new ValueProviderContext
            {
                Caller = caller,
                ExecutionContext = executionContext,
            },
            CancellationToken.None).ConfigureAwait(true) ?? string.Empty;
    }

    private static string ExtractMethod(string source, string marker) {
        int start = source.IndexOf(marker, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"Expected source to contain {marker}.");
        int end = source.IndexOf("    /// <summary>", start + marker.Length, StringComparison.Ordinal);
        end.ShouldBeGreaterThan(start, "Expected the next XML documentation block after the method.");
        return source[start..end];
    }
}
