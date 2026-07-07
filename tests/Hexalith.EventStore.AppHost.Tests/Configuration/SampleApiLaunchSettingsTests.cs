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

        string sampleApiPolicy = ExtractYamlPolicy(accessControl, "sample-api");

        sampleApiPolicy.ShouldContain("defaultAction: deny");
        sampleApiPolicy.ShouldNotContain("name: /**");
        sampleApiPolicy.ShouldNotContain("'GET'");
        sampleApiPolicy.ShouldNotContain("'PUT'");
        sampleApiPolicy.ShouldNotContain("'DELETE'");

        Dictionary<string, string> operations = ExtractOperationVerbs(sampleApiPolicy);
        operations.ShouldBe(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["/api/v1/queries"] = "['POST']",
            ["/api/v1/commands"] = "['POST']",
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

    private static string ExtractYamlPolicy(string yaml, string appId)
    {
        string marker = $"- appId: {appId}";
        int start = yaml.IndexOf(marker, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"Expected DAPR access control policy for {appId}.");
        int end = yaml.IndexOf("\n      - appId:", start + marker.Length, StringComparison.Ordinal);
        return end < 0 ? yaml[start..] : yaml[start..end];
    }

    private static Dictionary<string, string> ExtractOperationVerbs(string policy)
    {
        var operations = new Dictionary<string, string>(StringComparer.Ordinal);
        string? currentName = null;
        foreach (string rawLine in policy.Split('\n'))
        {
            string line = rawLine.Trim();
            const string NamePrefix = "- name: ";
            const string VerbPrefix = "httpVerb: ";
            if (line.StartsWith(NamePrefix, StringComparison.Ordinal))
            {
                currentName = line[NamePrefix.Length..];
                continue;
            }

            if (line.StartsWith(VerbPrefix, StringComparison.Ordinal) && currentName is not null)
            {
                operations[currentName] = line[VerbPrefix.Length..];
                currentName = null;
            }
        }

        return operations;
    }
}
