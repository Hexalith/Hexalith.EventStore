using System.Text.Json;
using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Measures strict security causes and independently pinned client-to-host rolling-version actor operations.</summary>
public sealed class PublishedSecurityAndCrossBoundaryTests
{
    /// <summary>Requires real opposite-version common and supported protected operations, keeping incapable endpoints nonpassing.</summary>
    [Fact]
    public async Task StrictSecurityAndOppositeVersionClientsHaveMeasuredBoundariesAsync()
    {
        await using var runtime = new QualificationRuntime();
        TestContext.Current.TestOutputHelper?.WriteLine("Invocation scratch: " + runtime.Root);
        var materializer = new ConsumerMaterializer(runtime, PublishedScenarioTests.FindRepository());
        await materializer.MaterializeAsync();
        materializer.Failures.ShouldBeEmpty();
        var observations = new List<ScenarioObservation>();
        PublishedScenarioTests.RegisterObservationLog(observations, Path.Combine(runtime.Root, "scenarios.ndjson"));
        await using (var cluster = new PublishedCluster(runtime, materializer.Builds))
        {
            await cluster.InitializeAsync();
            await cluster.ExecuteSecurityBoundariesAsync(observations);
            await cluster.ExecuteCrossClientCasesAsync(observations);
        }
        await runtime.CleanupAsync();
        await runtime.CleanupAsync();
        await File.WriteAllTextAsync(Path.Combine(runtime.Root, "focused-summary.json"), JsonSerializer.Serialize(observations));
        TestContext.Current.TestOutputHelper?.WriteLine(JsonSerializer.Serialize(observations));
        observations.Count.ShouldBe(10);
        observations.ShouldAllBe(observation => observation.Execution == "passed" && observation.Assertions > 0);
        observations.Where(observation => !observation.Case.Contains("3.70.1", StringComparison.Ordinal)).ShouldAllBe(observation => observation.Passing);
        runtime.CleanupFailures.ShouldBeEmpty();
    }
}
