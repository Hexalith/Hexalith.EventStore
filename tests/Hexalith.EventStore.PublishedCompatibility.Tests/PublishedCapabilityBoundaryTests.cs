using System.Text.Json;
using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Checks the selected published capability paths before another complete comparison matrix.</summary>
public sealed class PublishedCapabilityBoundaryTests
{
    /// <summary>Executes real status, signed handler/actor queries, persisted watermarks, registered evolution and natural delivery.</summary>
    [Fact]
    public async Task SelectedCapabilityOperationsHaveMeasuredBoundariesAsync()
    {
        await using var runtime = new QualificationRuntime();
        TestContext.Current.TestOutputHelper?.WriteLine("Invocation scratch: " + runtime.Root);
        var materializer = new ConsumerMaterializer(runtime, PublishedScenarioTests.FindRepository());
        await materializer.MaterializeAsync(prerequisiteOnly: true);
        materializer.Failures.ShouldBeEmpty();
        var observations = new List<ScenarioObservation>();
        await using (var cluster = new PublishedCluster(runtime, materializer.Builds))
        {
            await cluster.InitializeAsync();
            await cluster.ExecuteSelectedCapabilitiesAsync(observations);
        }
        await runtime.CleanupAsync();
        await runtime.CleanupAsync();
        TestContext.Current.TestOutputHelper?.WriteLine(JsonSerializer.Serialize(observations, new JsonSerializerOptions { WriteIndented = true }));
        await File.WriteAllTextAsync(Path.Combine(runtime.Root, "focused-summary.json"), JsonSerializer.Serialize(observations));
        observations.ShouldNotBeEmpty();
        observations.ShouldAllBe(observation => observation.Execution == "passed" && observation.Assertions > 0);
        observations.Where(observation => !observation.Case.EndsWith("-registered-manifest-production-hop", StringComparison.Ordinal))
            .ShouldAllBe(observation => observation.Passing);
        observations.Single(observation => observation.Case.EndsWith("-registered-manifest-production-hop", StringComparison.Ordinal))
            .Compatibility.ShouldBe("incompatible");
        runtime.CleanupFailures.ShouldBeEmpty();
    }
}
