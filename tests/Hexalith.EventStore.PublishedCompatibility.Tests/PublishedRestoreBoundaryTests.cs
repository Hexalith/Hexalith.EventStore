using System.Text.Json;
using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Independently executes all provider restore targets and the strengthened append/restart invariants.</summary>
public sealed class PublishedRestoreBoundaryTests
{
    /// <summary>Requires selected package/source recovery while retaining old-writer failures and containment observations.</summary>
    [Fact]
    public async Task ProviderRestoresPreserveRetainedStateThroughAppendAndRestartAsync()
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
            await cluster.ExecuteProviderRestoresAsync(observations);
        }
        await runtime.CleanupAsync();
        await runtime.CleanupAsync();
        await File.WriteAllTextAsync(Path.Combine(runtime.Root, "focused-summary.json"), JsonSerializer.Serialize(observations));
        TestContext.Current.TestOutputHelper?.WriteLine(JsonSerializer.Serialize(observations));
        observations.Count.ShouldBe(6);
        observations.ShouldAllBe(observation => observation.Assertions > 0 && observation.Execution != "unavailable");
        observations.Single(observation => observation.Scenario == "post-upgrade-restore" && observation.Case == ConsumerMaterializer.SelectedVersion).Passing.ShouldBeTrue();
        observations.Single(observation => observation.Scenario == "post-upgrade-restore" && observation.Case == "tagged-source").Passing.ShouldBeTrue();
        observations.Where(observation => observation.Scenario == "pre-upgrade-restore")
            .ShouldAllBe(observation => observation.Execution == "passed" && observation.Compatibility == "incompatible");
        runtime.CleanupFailures.ShouldBeEmpty();
    }
}
