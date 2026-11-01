using Shouldly;
using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Provides a focused live production-actor prerequisite before spending the complete matrix budget.</summary>
public sealed class PublishedActorPrerequisiteTests
{
    /// <summary>Starts only the immutable candidate consumers and requires a real committed actor command.</summary>
    [Fact]
    public async Task CandidateProductionActorCommitsBeforeQualificationAsync()
    {
        await using var runtime = new QualificationRuntime();
        TestContext.Current.TestOutputHelper?.WriteLine("Invocation scratch: " + runtime.Root);
        var materializer = new ConsumerMaterializer(runtime, PublishedScenarioTests.FindRepository());
        await materializer.MaterializeAsync(prerequisiteOnly: true);
        materializer.Failures.ShouldBeEmpty();
        materializer.Builds.Count.ShouldBe(3);
        var observations = new List<ScenarioObservation>();
        await using (var cluster = new PublishedCluster(runtime, materializer.Builds))
        {
            await PublishedScenarioTests.ObserveAsync(observations, "actual-domain-sidecar", "selected-package", async assertions =>
            {
                await cluster.InitializeAsync();
                assertions.Check(runtime.DaprLaunches.Count == 2, "actual-primary-and-selected-domain-sidecars-captured");
                return "compatible";
            });
        }
        await runtime.CleanupAsync();
        await runtime.CleanupAsync();
        await runtime.DisposeAsync();
        runtime.CleanupFailures.ShouldBeEmpty();
        var observation = observations.ShouldHaveSingleItem();
        observation.Passing.ShouldBeTrue();
        var launches = runtime.DaprLaunches;
        launches.Select(launch => launch.Role).Order().ShouldBe(new[] { "Domain", "Host" });
        launches.ShouldAllBe(launch => launch.Ended != null && launch.Ended >= launch.Started);
        HistoricalMatrixRuntimeInputContract.Validate(observation, runtime.Commands.Select(HistoricalExecutionCommand.Capture).ToArray(),
            runtime.ScenarioInputs, runtime.StateReads, launches, new HistoricalTransportIndex(runtime.HttpExchanges));
    }
}
