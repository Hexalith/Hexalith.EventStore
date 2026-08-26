namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using System.Text.Json;
using System.Xml.Linq;

using global::Aspire.Hosting;
using global::Aspire.Hosting.ApplicationModel;
using global::Aspire.Hosting.Testing;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.AppHost;
using Hexalith.EventStore.Aspire;

using YamlDotNet.RepresentationModel;

[Collection(AspireEnvironmentMutationCollection.Name)]
public class TenantsApiLaunchSettingsTests
{
    /// <summary>
    /// Verifies the ratified Tenants source revision exposes a usable Development launch profile
    /// for the API project consumed by the source-mode AppHost graph.
    /// </summary>
    [Fact]
    public void TenantsApiLaunchProfileProvidesDevelopmentHttpAndHttpsEndpoints()
    {
        string path = Path.Combine(
            RepositoryProjectPaths.GetRepositoryRoot(),
            "references",
            "Hexalith.Tenants",
            "src",
            "Hexalith.Tenants.Api",
            "Properties",
            "launchSettings.json");

        File.Exists(path).ShouldBeTrue();

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement profile = document.RootElement
            .GetProperty("profiles")
            .GetProperty("Hexalith.Tenants.Api");

        profile.GetProperty("commandName").GetString().ShouldBe("Project");
        profile.GetProperty("launchBrowser").GetBoolean().ShouldBeTrue();

        string applicationUrl = profile.GetProperty("applicationUrl").GetString().ShouldNotBeNull();
        string[] endpointTexts = applicationUrl.Split(
            ';',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        endpointTexts.Length.ShouldBe(2);

        Uri[] endpoints = endpointTexts.Select(endpointText =>
        {
            Uri.TryCreate(endpointText, UriKind.Absolute, out Uri? endpoint).ShouldBeTrue(
                $"The Tenants API launch endpoint '{endpointText}' must be an absolute URI.");
            return endpoint.ShouldNotBeNull();
        }).ToArray();

        endpoints.Select(endpoint => endpoint.Scheme).ShouldBe(["https", "http"], ignoreOrder: true);
        endpoints.All(endpoint => string.Equals(endpoint.Host, "localhost", StringComparison.Ordinal)).ShouldBeTrue();
        endpoints.All(endpoint => !endpoint.IsDefaultPort && endpoint.Port is > 0 and <= 65535).ShouldBeTrue();
        endpoints.Select(endpoint => endpoint.Port).Distinct().Count().ShouldBe(2);

        profile.GetProperty("environmentVariables")
            .GetProperty("ASPNETCORE_ENVIRONMENT")
            .GetString()
            .ShouldBe("Development");
    }

    [Fact]
    public void AppHostProject_ReferencesTenantsApiOnlyInTenantsSourceMode()
    {
        XDocument project = XDocument.Load(Path.Combine(
            RepositoryProjectPaths.GetRepositoryRoot(),
            "src",
            "Hexalith.EventStore.AppHost",
            "Hexalith.EventStore.AppHost.csproj"));

        XElement reference = project
            .Descendants()
            .Single(element => string.Equals(element.Name.LocalName, "ProjectReference", StringComparison.Ordinal)
                && (((string?)element.Attribute("Include"))?.Replace('\\', '/').EndsWith(
                    "Hexalith.Tenants.Api/Hexalith.Tenants.Api.csproj",
                    StringComparison.Ordinal) == true));

        ((string?)reference.Attribute("Condition")).ShouldBe("'$(HexalithTenantsFromSource)' == 'true'");
    }

    [Fact]
    public void AppHost_RegistersTenantsApiAsExternalServiceInvocationOnlyHost()
    {
        string program = ReadRepositorySource(Path.Combine(
            RepositoryProjectPaths.GetRepositoryRoot(),
            "src",
            "Hexalith.EventStore.AppHost",
            "Program.cs"));

        string tenantsApiBlock = ExtractBlock(
            program,
            "    _ = tenantsApi\n",
            "\n}\n\n// Add sample domain service");

        program.ShouldContain("tenantsApi = builder.AddProject<Projects.Hexalith_Tenants_Api>(\"tenants-api\");");
        program.ShouldContain("tenantsApi = builder.AddProject(\"tenants-api\", tenantsProjects.ApiProjectPath);");
        tenantsApiBlock.ShouldContain(".WithReference(eventStore)");
        tenantsApiBlock.ShouldContain(".WaitFor(eventStore)");
        tenantsApiBlock.ShouldContain(".WithExternalHttpEndpoints()");
        tenantsApiBlock.ShouldContain("AppId = \"tenants-api\"");
        tenantsApiBlock.ShouldContain("PlacementHostAddress = daprPlacementHostAddress");
        tenantsApiBlock.ShouldContain("SchedulerHostAddress = daprSchedulerHostAddress");
        tenantsApiBlock.ShouldNotContain("eventStoreResources.StateStore");
        tenantsApiBlock.ShouldNotContain("eventStoreResources.PubSub");
        tenantsApiBlock.ShouldNotContain(".WithReference(eventStoreResources");

        program.ShouldContain("_ = tenantsApi.WithEventStoreAuthenticationValidation(security);");
        program.ShouldNotContain("_ = tenantsApi.WithEventStoreClientCredentials(security);");
    }

    [Fact]
    public async Task AppHostModel_DefaultRunMode_RegistersResolvedTenantsHostsExactlyOnceWithExistingWiring()
    {
        string? originalSkipPrerequisiteCheck = Environment.GetEnvironmentVariable("SKIP_PREREQUISITE_CHECK");
        string? originalEnableKeycloak = Environment.GetEnvironmentVariable(
            HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey);

        try
        {
            Environment.SetEnvironmentVariable("SKIP_PREREQUISITE_CHECK", "true");
            Environment.SetEnvironmentVariable(
                HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey,
                "false");

            await using IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.Hexalith_EventStore_AppHost>()
                .ConfigureAwait(true);

            builder.ExecutionContext.IsRunMode.ShouldBeTrue();
            ProjectResource tenants = builder.Resources
                .OfType<ProjectResource>()
                .Where(static resource => string.Equals(resource.Name, "tenants", StringComparison.Ordinal))
                .ShouldHaveSingleItem();
            ProjectResource tenantsApi = builder.Resources
                .OfType<ProjectResource>()
                .Where(static resource => string.Equals(resource.Name, "tenants-api", StringComparison.Ordinal))
                .ShouldHaveSingleItem();

            TenantsProjectPaths resolvedPaths = TenantsProjectPaths.Resolve();
            Path.GetFullPath(tenants.GetProjectMetadata().ProjectPath)
                .ShouldBe(resolvedPaths.DomainServiceProjectPath);
            Path.GetFullPath(tenantsApi.GetProjectMetadata().ProjectPath)
                .ShouldBe(resolvedPaths.ApiProjectPath);

            IDaprSidecarResource tenantsSidecar = GetSidecar(tenants);
            DaprSidecarOptions tenantsSidecarOptions = GetOptions(tenantsSidecar);
            tenantsSidecarOptions.AppId.ShouldBe("tenants");
            tenantsSidecarOptions.EnableAppHealthCheck.ShouldBe(true);
            tenantsSidecarOptions.AppHealthCheckPath.ShouldBe("/alive");
            GetReferencedComponentNames(tenantsSidecar).ShouldBe(["pubsub", "statestore"]);
            GetReferencedResourceNames(tenants).ShouldContain("eventstore");
            GetWaitedResourceNames(tenants).ShouldContain("eventstore");

            IDaprSidecarResource tenantsApiSidecar = GetSidecar(tenantsApi);
            DaprSidecarOptions tenantsApiSidecarOptions = GetOptions(tenantsApiSidecar);
            tenantsApiSidecarOptions.AppId.ShouldBe("tenants-api");
            GetReferencedComponentNames(tenantsApiSidecar).ShouldBeEmpty();
            GetReferencedResourceNames(tenantsApi).ShouldContain("eventstore");
            GetWaitedResourceNames(tenantsApi).ShouldContain("eventstore");
        }
        finally
        {
            Environment.SetEnvironmentVariable("SKIP_PREREQUISITE_CHECK", originalSkipPrerequisiteCheck);
            Environment.SetEnvironmentVariable(
                HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey,
                originalEnableKeycloak);
        }
    }

    [Fact]
    public async Task AppHostModel_PublishMode_DoesNotRegisterPathDiscoveredTenantsHosts()
    {
        string? originalSkipPrerequisiteCheck = Environment.GetEnvironmentVariable("SKIP_PREREQUISITE_CHECK");
        string? originalEnableKeycloak = Environment.GetEnvironmentVariable(
            HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey);

        try
        {
            Environment.SetEnvironmentVariable("SKIP_PREREQUISITE_CHECK", "true");
            Environment.SetEnvironmentVariable(
                HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey,
                "false");

            await using IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.Hexalith_EventStore_AppHost>(["--AppHost:Operation=publish"])
                .ConfigureAwait(true);

            builder.ExecutionContext.IsPublishMode.ShouldBeTrue();
            ProjectResource[] tenantsResources =
            [
                .. builder.Resources
                    .OfType<ProjectResource>()
                    .Where(static resource => resource.Name is "tenants" or "tenants-api"),
            ];
            tenantsResources.ShouldAllBe(resource =>
                ReferenceEquals(
                    resource.GetProjectMetadata().GetType().Assembly,
                    typeof(Projects.Hexalith_EventStore_AppHost).Assembly),
                "Publish mode may contain explicit source-mode Projects.* resources, but never path-discovered resources.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("SKIP_PREREQUISITE_CHECK", originalSkipPrerequisiteCheck);
            Environment.SetEnvironmentVariable(
                HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey,
                originalEnableKeycloak);
        }
    }

    [Theory]
    [InlineData("domain-service")]
    [InlineData("api")]
    public void TenantsProjectPaths_WhenEitherHostIsMissing_FailsWithRootSubmoduleDiagnostic(string missingHost)
    {
        TenantsProjectPaths resolvedPaths = TenantsProjectPaths.Resolve();
        string missingPath = Path.Combine(
            RepositoryProjectPaths.GetRepositoryRoot(),
            ".missing-tenants-host",
            missingHost,
            "missing.csproj");
        TenantsProjectPaths paths = string.Equals(missingHost, "domain-service", StringComparison.Ordinal)
            ? resolvedPaths with { DomainServiceProjectPath = missingPath }
            : resolvedPaths with { ApiProjectPath = missingPath };

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(paths.Validate);

        exception.Message.ShouldContain(TenantsProjectPaths.SubmoduleInitializationCommand);
        exception.Message.ShouldContain(missingPath);
        exception.Message.ShouldNotContain(
            string.Equals(missingHost, "domain-service", StringComparison.Ordinal)
                ? resolvedPaths.ApiProjectPath
                : resolvedPaths.DomainServiceProjectPath);
    }

    [Fact]
    public void TenantsProjectPaths_ResolveUsesRootDeclaredSubmodule()
    {
        string repositoryRoot = RepositoryProjectPaths.GetRepositoryRoot();

        TenantsProjectPaths paths = TenantsProjectPaths.Resolve();

        paths.DomainServiceProjectPath.ShouldBe(Path.Combine(
            repositoryRoot,
            "references",
            "Hexalith.Tenants",
            "src",
            "Hexalith.Tenants",
            "Hexalith.Tenants.csproj"));
        paths.ApiProjectPath.ShouldBe(Path.Combine(
            repositoryRoot,
            "references",
            "Hexalith.Tenants",
            "src",
            "Hexalith.Tenants.Api",
            "Hexalith.Tenants.Api.csproj"));
    }

    [Fact]
    public void AppHostProgram_ExplicitSourceBranchUsesGeneratedTenantProjectsExactlyOnce()
    {
        string program = ReadRepositorySource(Path.Combine(
            RepositoryProjectPaths.GetRepositoryRoot(),
            "src",
            "Hexalith.EventStore.AppHost",
            "Program.cs"));

        CountOccurrences(
            program,
            "builder.AddProject<Projects.Hexalith_Tenants>(\"tenants\")").ShouldBe(1);
        CountOccurrences(
            program,
            "builder.AddProject<Projects.Hexalith_Tenants_Api>(\"tenants-api\")").ShouldBe(1);
    }

    [Fact]
    public void EventStoreAccessControl_TenantsApiPolicyDocumentsGatewayPostOperations()
    {
        var yaml = new YamlStream();
        using (var reader = File.OpenText(Path.Combine(
            RepositoryProjectPaths.GetRepositoryRoot(),
            "src",
            "Hexalith.EventStore.AppHost",
            "DaprComponents",
            "accesscontrol.yaml")))
        {
            yaml.Load(reader);
        }

        YamlMappingNode root = yaml.Documents.ShouldHaveSingleItem().RootNode.ShouldBeOfType<YamlMappingNode>();
        YamlMappingNode accessControl = Mapping(root, "spec", "accessControl");
        YamlMappingNode[] tenantsApiPolicies = Sequence(accessControl, "policies")
            .OfType<YamlMappingNode>()
            .Where(static policy => string.Equals(Scalar(policy, "appId"), "tenants-api", StringComparison.Ordinal))
            .ToArray();
        YamlMappingNode tenantsApiPolicy = tenantsApiPolicies.ShouldHaveSingleItem(
            "Expected exactly one DAPR access-control policy for tenants-api.");

        Scalar(tenantsApiPolicy, "defaultAction").ShouldBe("deny");
        var operations = new Dictionary<string, AccessControlOperation>(StringComparer.Ordinal);
        foreach (YamlMappingNode operation in Sequence(tenantsApiPolicy, "operations").OfType<YamlMappingNode>())
        {
            string name = Scalar(operation, "name");
            name.ShouldNotBe("/**", "tenants-api must not receive a wildcard EventStore invocation policy.");
            string[] verbs = Sequence(operation, "httpVerb")
                .OfType<YamlScalarNode>()
                .Select(static verb => verb.Value ?? string.Empty)
                .ToArray();
            verbs.ShouldBe(["POST"], "tenants-api may document only POST service-invocation operations.");
            operations
                .TryAdd(name, new AccessControlOperation(verbs.Single(), Scalar(operation, "action")))
                .ShouldBeTrue($"Duplicate DAPR ACL operation '{name}' must not be allowed to mask a broader rule.");
        }

        operations.ShouldBe(new Dictionary<string, AccessControlOperation>(StringComparer.Ordinal)
        {
            ["/api/v1/queries"] = new("POST", "allow"),
            ["/api/v1/commands"] = new("POST", "allow"),
        });
    }

    /// <summary>
    /// Reads a repository source file, normalizing CRLF to LF so markers containing "\n" match on any
    /// checkout. Kept in step with the identical helper in <see cref="SampleApiLaunchSettingsTests"/>.
    /// </summary>
    private static string ReadRepositorySource(string path)
        => File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string ExtractBlock(string text, string startMarker, string endMarker)
    {
        int start = text.IndexOf(startMarker, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"Expected to find '{startMarker}'.");
        int end = text.IndexOf(endMarker, start, StringComparison.Ordinal);
        end.ShouldBeGreaterThan(start, $"Expected to find '{endMarker}' after the Tenants API registration.");
        return text[start..end];
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int startIndex = 0;
        while ((startIndex = text.IndexOf(value, startIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += value.Length;
        }

        return count;
    }

    private static IDaprSidecarResource GetSidecar(ProjectResource project)
    {
        project.TryGetAnnotationsOfType<DaprSidecarAnnotation>(out IEnumerable<DaprSidecarAnnotation>? annotations)
            .ShouldBeTrue();
        return annotations!.ShouldHaveSingleItem().Sidecar;
    }

    private static DaprSidecarOptions GetOptions(IDaprSidecarResource sidecar)
    {
        sidecar.TryGetLastAnnotation<DaprSidecarOptionsAnnotation>(out DaprSidecarOptionsAnnotation? annotation)
            .ShouldBeTrue();
        return annotation!.Options;
    }

    private static string[] GetReferencedComponentNames(IDaprSidecarResource sidecar)
        => sidecar.TryGetAnnotationsOfType<DaprComponentReferenceAnnotation>(
            out IEnumerable<DaprComponentReferenceAnnotation>? annotations)
            ? [.. annotations.Select(static annotation => annotation.Component.Name).Order(StringComparer.Ordinal)]
            : [];

    private static string[] GetReferencedResourceNames(ProjectResource project)
        => [.. project.Annotations
            .OfType<ResourceRelationshipAnnotation>()
            .Where(static annotation => string.Equals(annotation.Type, "Reference", StringComparison.Ordinal))
            .Select(static annotation => annotation.Resource.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    private static string[] GetWaitedResourceNames(ProjectResource project)
        => [.. project.Annotations
            .OfType<WaitAnnotation>()
            .Select(static annotation => annotation.Resource.Name)
            .Order(StringComparer.Ordinal)];

    private static YamlMappingNode Mapping(YamlMappingNode root, params string[] path)
    {
        YamlNode current = root;
        foreach (string segment in path)
        {
            current = current.ShouldBeOfType<YamlMappingNode>().Children[new YamlScalarNode(segment)];
        }

        return current.ShouldBeOfType<YamlMappingNode>();
    }

    private static YamlSequenceNode Sequence(YamlMappingNode root, string key)
        => root.Children[new YamlScalarNode(key)].ShouldBeOfType<YamlSequenceNode>();

    private static string Scalar(YamlMappingNode root, string key)
        => root.Children[new YamlScalarNode(key)].ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty;

    private sealed record AccessControlOperation(string HttpVerb, string Action);
}
