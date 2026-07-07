namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using System.Xml.Linq;

using Hexalith.EventStore.Aspire;

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

        program.ShouldContain("_ = tenantsApi.WithEventStoreClientCredentials(security);");
    }

    [Fact]
    public void EventStoreAccessControl_TenantsApiPolicyDocumentsGatewayPostOperations()
    {
        string accessControl = File.ReadAllText(Path.Combine(
            RepositoryProjectPaths.GetRepositoryRoot(),
            "src",
            "Hexalith.EventStore.AppHost",
            "DaprComponents",
            "accesscontrol.yaml"));

        string[] tenantsApiPolicies = ExtractYamlPolicies(accessControl, "tenants-api");
        tenantsApiPolicies.Length.ShouldBe(1, "Expected exactly one DAPR access-control policy for tenants-api.");
        string tenantsApiPolicy = tenantsApiPolicies[0];

        tenantsApiPolicy.ShouldContain("defaultAction: deny");
        tenantsApiPolicy.ShouldNotContain("name: /**");
        Dictionary<string, AccessControlOperation> operations = ExtractOperations(tenantsApiPolicy);
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

    private static string[] ExtractYamlPolicies(string yaml, string appId)
    {
        string marker = $"- appId: {appId}";
        string nextPolicyMarker = "- appId:";
        string[] lines = yaml.Split('\n');
        var policies = new List<string>();
        for (int index = 0; index < lines.Length; index++)
        {
            if (!string.Equals(lines[index].Trim(), marker, StringComparison.Ordinal))
            {
                continue;
            }

            var policyLines = new List<string>();
            for (int policyIndex = index; policyIndex < lines.Length; policyIndex++)
            {
                if (policyIndex > index
                    && lines[policyIndex].TrimStart().StartsWith(nextPolicyMarker, StringComparison.Ordinal))
                {
                    break;
                }

                policyLines.Add(lines[policyIndex]);
            }

            policies.Add(string.Join('\n', policyLines));
        }

        return [.. policies];
    }

    private static Dictionary<string, AccessControlOperation> ExtractOperations(string policy)
    {
        var operations = new Dictionary<string, AccessControlOperation>(StringComparer.Ordinal);
        string? currentName = null;
        string? currentVerb = null;
        foreach (string rawLine in policy.Split('\n'))
        {
            string line = rawLine.Trim();
            const string NamePrefix = "- name: ";
            const string VerbPrefix = "httpVerb: ";
            const string ActionPrefix = "action: ";
            if (line.StartsWith(NamePrefix, StringComparison.Ordinal))
            {
                currentName = line[NamePrefix.Length..];
                currentVerb = null;
                continue;
            }

            if (line.StartsWith(VerbPrefix, StringComparison.Ordinal) && currentName is not null)
            {
                currentVerb = line[VerbPrefix.Length..];
                continue;
            }

            if (line.StartsWith(ActionPrefix, StringComparison.Ordinal)
                && currentName is not null
                && currentVerb is not null)
            {
                operations
                    .TryAdd(currentName, new AccessControlOperation(NormalizeHttpVerb(currentVerb), line[ActionPrefix.Length..]))
                    .ShouldBeTrue($"Duplicate DAPR ACL operation '{currentName}' must not be allowed to mask a broader rule.");
                currentName = null;
                currentVerb = null;
            }
        }

        return operations;
    }

    private static string NormalizeHttpVerb(string httpVerb)
    {
        string[] verbs = httpVerb
            .Trim()
            .Trim('[', ']')
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static verb => verb.Trim('\'', '"'))
            .ToArray();

        verbs.ShouldBe(["POST"], "tenants-api may document only POST service-invocation operations.");
        return verbs.Single();
    }

    private sealed record AccessControlOperation(string HttpVerb, string Action);
}
