using System.Text.Json;
using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Independently checks the reviewed provenance, constructor, signed principal and security-operation boundaries.</summary>
public sealed class PublishedAuditBoundaryTests
{
    /// <summary>Executes only the affected operations in all comparison and selected package/source consumers.</summary>
    [Fact]
    public async Task ReviewedPackageSourceBoundariesHaveCausalEvidenceAsync()
    {
        await using var runtime = new QualificationRuntime();
        TestContext.Current.TestOutputHelper?.WriteLine("Invocation scratch: " + runtime.Root);
        var materializer = new ConsumerMaterializer(runtime, PublishedScenarioTests.FindRepository());
        await materializer.MaterializeAsync();
        materializer.Failures.ShouldBeEmpty();
        var observations = new List<ScenarioObservation>();
        PublishedScenarioTests.RegisterObservationLog(observations, Path.Combine(runtime.Root, "scenarios.ndjson"));
        foreach (ConsumerBuild build in materializer.Builds)
            await PublishedScenarioTests.ObserveAsync(observations, "provenance", build.Version + "-" + build.Configuration + "-" + build.Role, async assertions =>
            {
                JsonElement result = await PublishedScenarioTests.ProbeAsync(runtime, build, new { operation = "identity" });
                PublishedScenarioTests.ValidateIdentity(build, result, assertions);
                return "compatible";
            });
        await PublishedScenarioTests.ExecuteLegacyConstructorsAsync(runtime, materializer.Builds, observations);
        foreach (ConsumerBuild probe in materializer.Builds.Where(build => build.Role == "Probe" && build.Version == ConsumerMaterializer.SelectedVersion))
            await PublishedScenarioTests.ObserveAsync(observations, "query-wire", probe.Configuration + "-signed-dual-principal", async assertions =>
            {
                JsonElement result = await PublishedScenarioTests.ProbeAsync(runtime, probe, new { operation = "signed-query" });
                foreach (string check in new[] { "verified", "principalsBound", "missingRefused", "forgedRefused", "foreignTenantRefused" })
                    assertions.Check(result.GetProperty(check).GetBoolean(), "signed-query-" + check);
                assertions.Record("signed-dual-principal-query", result.GetProperty("value").GetRawText());
                return "compatible";
            });
        await using (var cluster = new PublishedCluster(runtime, materializer.Builds))
        {
            await cluster.InitializeAsync();
            await cluster.ExecuteSecurityBoundariesAsync(observations);
            await cluster.ExecuteSelectedMalformedBoundariesAsync(observations);
        }
        await runtime.CleanupAsync();
        await runtime.CleanupAsync();
        await File.WriteAllTextAsync(Path.Combine(runtime.Root, "focused-summary.json"), JsonSerializer.Serialize(observations));
        TestContext.Current.TestOutputHelper?.WriteLine(JsonSerializer.Serialize(observations));
        observations.Count.ShouldBe(38);
        observations.ShouldAllBe(observation => observation.Execution == "passed" && observation.Assertions > 0);
        observations.Where(observation => !observation.Case.StartsWith("3.70.1", StringComparison.Ordinal)).ShouldAllBe(observation => observation.Passing);
        runtime.CleanupFailures.ShouldBeEmpty();
    }
}
