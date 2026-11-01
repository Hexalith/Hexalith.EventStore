using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Exercises real final cleanup, idempotent disposal and actual launch deadline phases.</summary>
public sealed class QualificationLifecycleFinalizationTests
{
    [Fact]
    public async Task FinalDisposalIsReportedOnceAndRepeatedDisposalDoesNotAppendEvidence()
    {
        await using var runtime = new QualificationRuntime();
        await runtime.CleanupAsync();
        await runtime.CleanupAsync();
        string summary = await PublishedScenarioTests.WriteSummaryAsync(runtime, [], "Synthetic nonqualifying finalization control.");
        runtime.Disposed.ShouldBeTrue();
        runtime.CleanupPasses.Count.ShouldBe(3);
        runtime.CleanupPasses.Last().Final.ShouldBeTrue();
        using var document = JsonDocument.Parse(summary);
        document.RootElement.GetProperty("disposed").GetBoolean().ShouldBeTrue();
        JsonElement.DeepEquals(document.RootElement.GetProperty("cleanupPasses"), JsonSerializer.SerializeToElement(runtime.CleanupPasses)).ShouldBeTrue();
        int commands = runtime.Commands.Count;
        await runtime.DisposeAsync();
        await runtime.DisposeAsync();
        runtime.Commands.Count.ShouldBe(commands);
        runtime.CleanupPasses.Count.ShouldBe(3);
    }

    [Fact]
    public async Task FinalCleanupFailureCannotDisappearFromSummaryOrChildImport()
    {
        await using var sentinelOwner = new QualificationRuntime();
        string sentinel = sentinelOwner.ReserveContainer("final-disposal-sentinel");
        await sentinelOwner.RequireAsync("docker", ["run", "-d", "--name", sentinel, "--label", "p1r.owner=" + sentinelOwner.Id, PublishedCluster.RedisImage]);
        var child = new QualificationRuntime();
        try
        {
            await child.CaptureSharedContainersAsync();
            await child.CleanupAsync();
            await child.CleanupAsync();
            child.CleanupFailures.ShouldBeEmpty();
            await sentinelOwner.RequireAsync("docker", ["stop", sentinel]);
            await Should.ThrowAsync<InvalidOperationException>(() => child.DisposeAsync().AsTask());
            child.CleanupPasses.Last().Final.ShouldBeTrue();
            child.CleanupPasses.Last().Failures.ShouldBeGreaterThan(0);
            string summary = await PublishedScenarioTests.WriteSummaryAsync(child, [], "Actual final shared-state refusal control.");
            using var document = JsonDocument.Parse(summary);
            document.RootElement.GetProperty("qualified").GetBoolean().ShouldBeFalse();
            document.RootElement.GetProperty("failed").GetInt32().ShouldBe(1);
            document.RootElement.GetProperty("cleanupPasses")[2].GetProperty("Failures").GetInt32().ShouldBeGreaterThan(0);
            var parent = new QualificationRuntime();
            try
            {
                Should.Throw<InvalidOperationException>(() => parent.ImportCompletedRuntime(child));
                parent.CleanupFailures.ShouldNotBeEmpty();
                parent.CleanupPasses.ShouldContain(pass => pass.Owner == child.Id && pass.Final && pass.Failures > 0);
            }
            finally { await Should.ThrowAsync<InvalidOperationException>(() => parent.DisposeAsync().AsTask()); }
        }
        finally { await Should.ThrowAsync<InvalidOperationException>(() => child.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObservationBeforeDeadlinePreservesActualTimeoutAndCancellationCauses(bool cancel)
    {
        await using var runtime = new QualificationRuntime();
        string marker = Path.Combine(runtime.Root, "observed-marker");
        var process = runtime.Start(new("python3", ["-c", "import pathlib,sys,time; pathlib.Path(sys.argv[1]).write_text('ready'); time.sleep(120)", marker], runtime.Root, TimeSpan.FromMilliseconds(100)));
        using var observation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        observation.CancelAfter(TimeSpan.FromSeconds(5));
        while (!File.Exists(marker)) await Task.Delay(25, observation.Token);
        // Actual pre-observation may exceed execution's deadline: it has its own existing finite budget.
        await Task.Delay(150, observation.Token);
        process.Process.HasExited.ShouldBeFalse();
        using var cancelled = new CancellationTokenSource();
        Task<CommandObservation> completion = runtime.CompleteStartedAsync(process, cancelled.Token);
        if (cancel) cancelled.Cancel();
        if (cancel) await Should.ThrowAsync<OperationCanceledException>(() => completion);
        else await Should.ThrowAsync<TimeoutException>(() => completion);
        var captured = runtime.Commands.ShouldHaveSingleItem();
        captured.Interruption.ShouldBe(cancel ? "cancelled" : "timeout");
        captured.DeadlineStarted!.Value.ShouldBeGreaterThan(process.Started.AddMilliseconds(100));
        process.Process.HasExited.ShouldBeTrue();
    }
}
