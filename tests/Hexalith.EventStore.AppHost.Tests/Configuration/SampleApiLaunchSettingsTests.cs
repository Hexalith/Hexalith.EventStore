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
}
