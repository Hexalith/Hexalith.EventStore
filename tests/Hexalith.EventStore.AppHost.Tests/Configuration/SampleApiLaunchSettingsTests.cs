namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using System.Text.Json;

using Hexalith.EventStore.Aspire;

public class SampleApiLaunchSettingsTests
{
    [Fact]
    public void LaunchSettings_HttpsProfile_ProvidesDevelopmentHttpEndpoint()
    {
        string path = Path.Combine(
            RepositoryProjectPaths.GetRepositoryRoot(),
            "samples",
            "Hexalith.EventStore.Sample.Api",
            "Properties",
            "launchSettings.json");

        File.Exists(path).ShouldBeTrue();

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement profile = document.RootElement
            .GetProperty("profiles")
            .GetProperty("https");

        profile.GetProperty("commandName").GetString().ShouldBe("Project");

        string applicationUrl = profile.GetProperty("applicationUrl").GetString()!;
        applicationUrl.ShouldContain("https://localhost:");
        applicationUrl.ShouldContain("http://localhost:");

        profile.GetProperty("environmentVariables")
            .GetProperty("ASPNETCORE_ENVIRONMENT")
            .GetString()
            .ShouldBe("Development");
    }

    [Fact]
    public void AppHost_RegistersSampleApiAsExternalServiceInvocationOnlyHost()
    {
        string program = File.ReadAllText(Path.Combine(
            RepositoryProjectPaths.GetRepositoryRoot(),
            "src",
            "Hexalith.EventStore.AppHost",
            "Program.cs"));

        string sampleApiBlock = ExtractBlock(program, "IResourceBuilder<ProjectResource> sampleApi =");

        sampleApiBlock.ShouldContain("builder.AddProject<Projects.Hexalith_EventStore_Sample_Api>(\"sample-api\")");
        sampleApiBlock.ShouldContain(".WithReference(eventStore)");
        sampleApiBlock.ShouldContain(".WaitFor(eventStore)");
        sampleApiBlock.ShouldContain(".WithExternalHttpEndpoints()");
        sampleApiBlock.ShouldContain("AppId = \"sample-api\"");
        sampleApiBlock.ShouldContain("PlacementHostAddress = daprPlacementHostAddress");
        sampleApiBlock.ShouldContain("SchedulerHostAddress = daprSchedulerHostAddress");
        sampleApiBlock.ShouldNotContain("eventStoreResources.StateStore");
        sampleApiBlock.ShouldNotContain("eventStoreResources.PubSub");
        sampleApiBlock.ShouldNotContain(".WithReference(eventStoreResources");

        program.ShouldContain("_ = sampleApi.WithEventStoreClientCredentials(security);");
    }

    [Fact]
    public void EventStoreAccessControl_SampleApiPolicyNarrowsGatewayPostOperations()
    {
        string accessControl = File.ReadAllText(Path.Combine(
            RepositoryProjectPaths.GetRepositoryRoot(),
            "src",
            "Hexalith.EventStore.AppHost",
            "DaprComponents",
            "accesscontrol.yaml"));

        string[] sampleApiPolicies = ExtractYamlPolicies(accessControl, "sample-api");
        sampleApiPolicies.Length.ShouldBe(1, "Expected exactly one DAPR access-control policy for sample-api.");
        string sampleApiPolicy = sampleApiPolicies[0];

        sampleApiPolicy.ShouldContain("defaultAction: deny");
        sampleApiPolicy.ShouldNotContain("name: /**");
        sampleApiPolicy.ShouldNotContain("'GET'");
        sampleApiPolicy.ShouldNotContain("'PUT'");
        sampleApiPolicy.ShouldNotContain("'DELETE'");

        Dictionary<string, AccessControlOperation> operations = ExtractOperations(sampleApiPolicy);
        operations.ShouldBe(new Dictionary<string, AccessControlOperation>(StringComparer.Ordinal)
        {
            ["/api/v1/queries"] = new("['POST']", "allow"),
            ["/api/v1/commands"] = new("['POST']", "allow"),
        });
    }

    private static string ExtractBlock(string text, string startMarker)
    {
        int start = text.IndexOf(startMarker, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"Expected to find '{startMarker}'.");
        int end = text.IndexOf(";\n\nif (security is not null)", start, StringComparison.Ordinal);
        end.ShouldBeGreaterThan(start, "Expected sample-api resource registration assignment before the security block.");
        end += 1;
        return text[start..end];
    }

    private static string[] ExtractYamlPolicies(string yaml, string appId)
    {
        string marker = $"- appId: {appId}";
        var policies = new List<string>();
        int searchFrom = 0;
        while (true)
        {
            int start = yaml.IndexOf(marker, searchFrom, StringComparison.Ordinal);
            if (start < 0)
            {
                break;
            }

            int end = yaml.IndexOf("\n      - appId:", start + marker.Length, StringComparison.Ordinal);
            policies.Add(end < 0 ? yaml[start..] : yaml[start..end]);
            searchFrom = start + marker.Length;
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
                operations[currentName] = new AccessControlOperation(currentVerb, line[ActionPrefix.Length..]);
                currentName = null;
                currentVerb = null;
            }
        }

        return operations;
    }

    private sealed record AccessControlOperation(string HttpVerb, string Action);
}
