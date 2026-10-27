using System.Text.Json;
using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Measures the actual required production hop for the selected package and exact tagged source independently.</summary>
public sealed class PublishedManifestBoundaryTests
{
    /// <summary>Requires a causal missing-callable refusal and byte preservation in both origins without claiming compatibility.</summary>
    [Fact]
    public async Task RegisteredProductionHopRetainsCausalPackageAndSourceRefusalsAsync()
    {
        await using var runtime = new QualificationRuntime();
        TestContext.Current.TestOutputHelper?.WriteLine("Invocation scratch: " + runtime.Root);
        var materializer = new ConsumerMaterializer(runtime, PublishedScenarioTests.FindRepository());
        await materializer.MaterializeAsync(selectedPairOnly: true);
        materializer.Failures.ShouldBeEmpty();
        var observations = new List<ScenarioObservation>();
        PublishedScenarioTests.RegisterObservationLog(observations, Path.Combine(runtime.Root, "scenarios.ndjson"));
        await using (var cluster = new PublishedCluster(runtime, materializer.Builds))
        {
            await cluster.InitializeAsync();
            await cluster.ExecuteSelectedManifestAsync(observations);
        }
        await runtime.CleanupAsync();
        await runtime.CleanupAsync();
        await File.WriteAllTextAsync(Path.Combine(runtime.Root, "focused-summary.json"), JsonSerializer.Serialize(observations));
        TestContext.Current.TestOutputHelper?.WriteLine(JsonSerializer.Serialize(observations));
        observations.Count.ShouldBe(2);
        observations.ShouldAllBe(observation => observation.Execution == "passed" && observation.Assertions > 0
            && observation.Compatibility == "incompatible" && !observation.Passing);
        runtime.CleanupFailures.ShouldBeEmpty();
    }
}
