using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Validates lifecycle evidence against genuinely interrupted owned providers and repeated daemon observations.</summary>
public sealed class QualificationProviderLifecycleTests
{
    /// <summary>The actual interrupted launcher, daemon image binding and independently repeated absence satisfy the contract.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterruptedProviderUsesItsObservedDaemonImageAndActualCleanup(bool cancel)
    {
        await using var runtime = new QualificationRuntime();
        var assertions = new MeasuredAssertions();
        DateTimeOffset began = DateTimeOffset.UtcNow;
        await ContainerCreationDrill.ExecuteAsync(runtime, cancel, assertions);
        await runtime.DisposeAsync(); // The real child finally contributes its third disposal pass.
        assertions.Record("cleanup-runtime", JsonSerializer.Serialize(new { owner = runtime.Id, root = runtime.Root }));
        await using var parent = new QualificationRuntime();
        parent.ImportCompletedRuntime(runtime);
        assertions.Failures.ShouldBeEmpty();
        var observation = new ScenarioObservation("failure-cleanup", cancel ? "cancellation-and-startup-failure" : "timeout-and-detached-descendant",
            "passed", "compatible", assertions.Count, assertions.Failures, Measurements: assertions.Measurements,
            Started: began, Ended: DateTimeOffset.UtcNow);
        HistoricalMatrixCleanupContract.Validate(observation, assertions.Measurements, runtime.Commands.Select(HistoricalExecutionCommand.Capture).ToArray());
        HistoricalMatrixCleanupSequenceContract.Validate(observation, assertions.Measurements,
            parent.Commands.Select(HistoricalExecutionCommand.Capture).ToArray(), parent.CleanupPasses, parent.ProcessScans);
        runtime.CleanupFailures.ShouldBeEmpty();
    }

    /// <summary>Both actual cleanup passes must retain daemon scans, exact owner removals and shared container state.</summary>
    [Fact]
    public async Task RepeatedCleanupReconstructsActualContainerAndVolumeRemoval()
    {
        await using var runtime = new QualificationRuntime();
        await runtime.CaptureSharedContainersAsync();
        string container = runtime.ReserveContainer("actual-ledger"), volume = runtime.ReserveVolume("actual-ledger-volume");
        await runtime.RequireAsync("docker", ["volume", "create", "--label", "p1r.owner=" + runtime.Id, volume]);
        await runtime.RequireAsync("docker", ["create", "--name", container, "--label", "p1r.owner=" + runtime.Id, PublishedCluster.RedisImage]);
        DateTimeOffset began = DateTimeOffset.UtcNow;
        await runtime.CleanupAsync();
        await runtime.CleanupAsync();
        runtime.CleanupFailures.ShouldBeEmpty();
        var records = new Dictionary<string, string> { ["cleanup-runtime"] = JsonSerializer.Serialize(new { owner = runtime.Id, root = runtime.Root }) };
        var observation = new ScenarioObservation("failure-cleanup", "matrix-cleanup-twice", "passed", "compatible", 1, [],
            Measurements: records, Started: began, Ended: DateTimeOffset.UtcNow);
        HistoricalMatrixCleanupSequenceContract.Validate(observation, records,
            runtime.Commands.Select(HistoricalExecutionCommand.Capture).ToArray(), runtime.CleanupPasses, runtime.ProcessScans);
    }
}
