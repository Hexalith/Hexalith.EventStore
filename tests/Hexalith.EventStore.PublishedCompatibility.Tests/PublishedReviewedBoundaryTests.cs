using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Measures the review-derived production handler and Scheduler boundaries independently for package and tagged source.</summary>
public sealed class PublishedReviewedBoundaryTests
{
    /// <summary>Uses real production Gateway discovery/invocation and naturally due reminders in both immutable origins.</summary>
    [Fact]
    public async Task DeclaredHandlerTransportAndNaturalReminderHaveIndependentOriginEvidenceAsync()
    {
        await using var runtime = new QualificationRuntime();
        TestContext.Current.TestOutputHelper?.WriteLine("Invocation scratch: " + runtime.Root);
        string repository = PublishedScenarioTests.FindRepository();
        var materializer = new ConsumerMaterializer(runtime, repository);
        await materializer.MaterializeAsync(selectedPairOnly: true);
        materializer.Failures.ShouldBeEmpty();
        materializer.Builds.Count.ShouldBe(6);
        foreach (ConsumerBuild build in materializer.Builds)
        {
            build.SelectedSdk.ShouldNotBeNullOrWhiteSpace();
            build.SdkPolicyHash.ShouldNotBeNullOrWhiteSpace();
            byte[] sdkPolicy = await File.ReadAllBytesAsync(Path.Combine(Path.GetDirectoryName(build.Project)!, "global.json"));
            QualificationRuntime.Hash(sdkPolicy).ShouldBe(build.SdkPolicyHash);
            ConsumerMaterializer.ValidateSdkPolicy(sdkPolicy, build.SelectedSdk, taggedSource: build.Source is not null);
            string expectedRoot = build.Source is null ? repository : Path.Combine(runtime.Root, "tagged-source");
            string expectedPolicy = Path.Combine(expectedRoot, "global.json");
            build.SdkPolicyOrigin.ShouldBe(expectedPolicy);
            QualificationRuntime.Hash(await File.ReadAllBytesAsync(expectedPolicy)).ShouldBe(build.SdkPolicyHash);
            Path.GetFileName(Path.GetDirectoryName(build.Assembly)).ShouldBe(build.Source is null ? "net11.0" : "net10.0");
        }
        var observations = new List<ScenarioObservation>();
        PublishedScenarioTests.RegisterObservationLog(observations, Path.Combine(runtime.Root, "scenarios.ndjson"));
        await using (var cluster = new PublishedCluster(runtime, materializer.Builds))
        {
            await cluster.InitializeAsync();
            await cluster.ExecuteReviewedQueryAndReminderBoundariesAsync(observations);
        }
        await runtime.CleanupAsync();
        await runtime.CleanupAsync();
        await File.WriteAllTextAsync(Path.Combine(runtime.Root, "focused-summary.json"), JsonSerializer.Serialize(observations));
        TestContext.Current.TestOutputHelper?.WriteLine(JsonSerializer.Serialize(observations));
        observations.Count.ShouldBe(8);
        observations.ShouldAllBe(observation => observation.Passing && observation.Assertions > 0);
        runtime.Commands.ShouldAllBe(command => !command.OutputTruncated);
        runtime.CleanupFailures.ShouldBeEmpty();
    }
}
