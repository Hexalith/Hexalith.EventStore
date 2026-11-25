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
        string program = ReadRepositorySource(Path.Combine(
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

        program.ShouldContain("_ = sampleApi.WithEventStoreJwtAuthentication(security, jwtAuthentication);");
        program.ShouldNotContain("sampleApi.WithEventStoreClientCredentials(");

        // The block above ends at the security section, so it cannot see a later grant. These whole-file
        // guards keep the "zero direct infrastructure access" invariant enforced across all of Program.cs.
        program.ShouldNotContain("sampleApi.WithReference(eventStoreResources");
        program.ShouldNotContain("sampleApi.WithReference(eventStore.StateStore");
        program.ShouldNotContain("sampleApi.WithReference(eventStore.PubSub");
    }

    [Fact]
    public void ReadRepositorySource_NormalizesCrlfSourceToLf()
    {
        // Pins the read-site normalization: without it, every marker containing "\n" silently stops
        // matching on a CRLF working tree, which no CI lane can reproduce (the index is LF).
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            File.WriteAllText(path, "sampleApi;\r\n\r\nif (security is not null)\r\n");

            ReadRepositorySource(path).ShouldBe("sampleApi;\n\nif (security is not null)\n");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void ExtractBlock_ReturnsTheSameBlockForEitherLineEnding(string lineEnding)
    {
        string source = string.Join(lineEnding, [
            "IResourceBuilder<ProjectResource> sampleApi =",
            "    builder.AddProject<Projects.Hexalith_EventStore_Sample_Api>(\"sample-api\");",
            string.Empty,
            "var jwtAuthentication = new HexalithEventStoreJwtAuthenticationOptions();",
        ]);

        string block = ExtractBlock(
            source.Replace("\r\n", "\n", StringComparison.Ordinal),
            "IResourceBuilder<ProjectResource> sampleApi =");

        block.ShouldBe(
            "IResourceBuilder<ProjectResource> sampleApi =\n"
            + "    builder.AddProject<Projects.Hexalith_EventStore_Sample_Api>(\"sample-api\");");
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void EventStoreAccessControl_SampleApiPolicyDocumentsGatewayPostOperations(string lineEnding)
        => DaprAccessControlAssertions.AssertGatewayPostOperations(
            DaprAccessControlAssertions.ReadConfiguration().ReplaceLineEndings(lineEnding), "sample-api");

    /// <summary>
    /// Reads a repository source file, normalizing CRLF to LF so markers containing "\n" match on any
    /// checkout. Normalization happens here rather than in the block extractors so callers that assert
    /// against the whole file get the same line endings as callers that assert against an extracted block.
    /// </summary>
    private static string ReadRepositorySource(string path)
        => File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string ExtractBlock(string text, string startMarker)
    {
        int start = text.IndexOf(startMarker, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"Expected to find '{startMarker}'.");
        int end = text.IndexOf(";\n\n", start, StringComparison.Ordinal);
        end.ShouldBeGreaterThan(start, "Expected sample-api resource registration assignment before the security block.");
        end += 1;
        return text[start..end];
    }
}
