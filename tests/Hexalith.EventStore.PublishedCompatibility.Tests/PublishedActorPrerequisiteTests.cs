using Shouldly;

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
        await using (var cluster = new PublishedCluster(runtime, materializer.Builds))
        {
            await cluster.InitializeAsync();
        }
        await runtime.CleanupAsync();
        await runtime.CleanupAsync();
        runtime.CleanupFailures.ShouldBeEmpty();
    }
}
