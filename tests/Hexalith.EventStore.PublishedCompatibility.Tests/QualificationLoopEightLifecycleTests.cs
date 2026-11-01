using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Exercises actual cancellation and shared-state changes through the same finalized outer matrix path.</summary>
public sealed class QualificationLoopEightLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OuterFailureAlwaysWritesFinalPartialReceiptAndPreservesPrimary(bool cancel)
    {
        var runtime = new QualificationRuntime(); var rows = new List<ScenarioObservation>();
        Exception? original = null;
        async Task Work()
        {
            if (!cancel) { original = new InvalidOperationException("Synthetic outer failure before full execution."); throw original; }
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            try { await runtime.RunAsync(new("python3", ["-c", "import time; time.sleep(120)"], runtime.Root, TimeSpan.FromSeconds(20)), cancellation.Token); }
            catch (OperationCanceledException error) { original = error; throw; }
        }
        Exception? thrown = null;
        try { await PublishedScenarioTests.RunFinalizedAsync(runtime, rows, Work); }
        catch (Exception error) { thrown = error; }
        thrown.ShouldNotBeNull();
        thrown.ShouldBeSameAs(original);
        runtime.Disposed.ShouldBeTrue();
        runtime.CleanupPasses.Last().Final.ShouldBeTrue();
        runtime.CleanupPasses.Last().RemainingProcesses.ShouldBe(0);
        using var summary = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(runtime.Root, "summary.json")));
        summary.RootElement.GetProperty("incomplete").GetBoolean().ShouldBeTrue();
        summary.RootElement.GetProperty("qualified").GetBoolean().ShouldBeFalse();
        summary.RootElement.GetProperty("observations").GetArrayLength().ShouldBeLessThan(239);
        JsonElement.DeepEquals(summary.RootElement.GetProperty("executionCommands"), JsonSerializer.SerializeToElement(runtime.Commands.Select(HistoricalExecutionCommand.Capture).ToArray())).ShouldBeTrue();
        if (cancel) runtime.Commands.Single().Interruption.ShouldBe("cancelled");
        await runtime.DisposeAsync();
    }

    [Fact]
    public async Task NormallyCompletedSubsetIsStillAnIncompleteNonqualifyingMatrix()
    {
        var runtime = new QualificationRuntime();
        await PublishedScenarioTests.RunFinalizedAsync(runtime, [], () => Task.CompletedTask);
        using var summary = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(runtime.Root, "summary.json")));
        summary.RootElement.GetProperty("incomplete").GetBoolean().ShouldBeTrue();
        summary.RootElement.GetProperty("executionInterrupted").GetBoolean().ShouldBeFalse();
        summary.RootElement.GetProperty("qualified").GetBoolean().ShouldBeFalse();
        runtime.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task FirstSharedBaselineSurvivesTheSecondDrillCaptureAndFinalFailure()
    {
        await using var sentinelOwner = new QualificationRuntime();
        string name = sentinelOwner.ReserveContainer("first-baseline-sentinel");
        await sentinelOwner.RequireAsync("docker", ["run", "-d", "--name", name, "--label", "p1r.owner=" + sentinelOwner.Id, PublishedCluster.RedisImage]);
        var runtime = new QualificationRuntime();
        try
        {
            await runtime.CaptureSharedContainersAsync();
            DateTimeOffset firstCompleted = DateTimeOffset.UtcNow;
            await sentinelOwner.RequireAsync("docker", ["stop", name]);
            await runtime.CaptureSharedContainersAsync();
            var primary = new InvalidOperationException("Synthetic primary failure after an actual shared-state change.");
            var thrown = await Should.ThrowAsync<InvalidOperationException>(() => PublishedScenarioTests.RunFinalizedAsync(runtime, [], () => Task.FromException(primary)));
            thrown.ShouldBeSameAs(primary);
            var final = runtime.CleanupPasses.Last();
            final.Final.ShouldBeTrue(); final.Failures.ShouldBeGreaterThan(0); final.BaselineEnded!.Value.ShouldBeLessThanOrEqualTo(firstCompleted);
            runtime.CleanupFailures.ShouldContain(value => value.Contains("Shared container identity/state changed", StringComparison.Ordinal));
            using var summary = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(runtime.Root, "summary.json")));
            summary.RootElement.GetProperty("qualified").GetBoolean().ShouldBeFalse();
            summary.RootElement.GetProperty("failed").GetInt32().ShouldBe(1);
        }
        finally { await Should.ThrowAsync<InvalidOperationException>(() => runtime.DisposeAsync().AsTask()); }
    }
}
