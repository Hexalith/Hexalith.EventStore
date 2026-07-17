namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using System.Xml.Linq;

using Hexalith.EventStore.Aspire;

using YamlDotNet.RepresentationModel;

public class TenantsApiLaunchSettingsTests
{
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
        string program = File.ReadAllText(Path.Combine(
            RepositoryProjectPaths.GetRepositoryRoot(),
            "src",
            "Hexalith.EventStore.AppHost",
            "Program.cs"));

        string tenantsApiBlock = ExtractBlock(program, "IResourceBuilder<ProjectResource> tenantsApi =");

        tenantsApiBlock.ShouldContain("builder.AddProject<Projects.Hexalith_Tenants_Api>(\"tenants-api\")");
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

    private static string ExtractBlock(string text, string startMarker)
    {
        int start = text.IndexOf(startMarker, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"Expected to find '{startMarker}'.");
        int end = text.IndexOf("#endif", start, StringComparison.Ordinal);
        end.ShouldBeGreaterThan(start, "Expected tenants-api resource registration before the source-mode #endif.");
        return text[start..end];
    }

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
