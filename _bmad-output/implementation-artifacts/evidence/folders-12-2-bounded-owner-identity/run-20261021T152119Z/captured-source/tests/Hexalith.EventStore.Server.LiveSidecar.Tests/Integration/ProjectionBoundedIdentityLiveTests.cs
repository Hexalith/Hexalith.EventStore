using System.Diagnostics;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Qualifies complete original owners across durable staging and promoted checkpoint handoff process replacement.</summary>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public sealed class ProjectionBoundedIdentityLiveTests(DaprTestContainerFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FocusedProcessReplacementExercisesPartialStageAndOwnerHandoff(bool bufferedOwner)
        => ProductionDispatcherResumesStagingAndPromotedHandoffAcrossDistinctProcesses(129, bufferedOwner);

    [Theory]
    [InlineData(10_001, false)]
    [InlineData(32_769, false)]
    [InlineData(10_001, true)]
    [InlineData(32_769, true)]
    public async Task ProductionDispatcherResumesStagingAndPromotedHandoffAcrossDistinctProcesses(int count, bool bufferedOwner)
    {
        fixture.ThrowIfHostStopped();
        string tenant = "bounded-" + Guid.NewGuid().ToString("N");
        string directory = Environment.GetEnvironmentVariable("BOUNDED_IDENTITY_EVIDENCE_DIR")
            ?? Path.Combine(Path.GetTempPath(), "bounded-identity-" + tenant);
        Directory.CreateDirectory(directory);
        var pids = new List<int>();
        JsonArray? runtime = null;
        foreach (string phase in new[] { "stage", "promote", "resume" })
        {
            string prefix = Path.Combine(directory, (bufferedOwner ? "buffered-" : "streamed-") + count + "-" + phase);
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(typeof(ProjectionBoundedIdentityProcessWorkerTests).Assembly.Location);
            start.ArgumentList.Add("-class");
            start.ArgumentList.Add(typeof(ProjectionBoundedIdentityProcessWorkerTests).FullName!);
            start.Environment["BOUNDED_IDENTITY_PHASE"] = phase;
            start.Environment["BOUNDED_IDENTITY_BUFFERED"] = bufferedOwner ? "true" : "false";
            start.Environment["BOUNDED_IDENTITY_TENANT"] = tenant;
            start.Environment["BOUNDED_IDENTITY_ENDPOINT"] = fixture.DaprGrpcEndpoint;
            start.Environment["BOUNDED_IDENTITY_COUNT"] = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            start.Environment["BOUNDED_IDENTITY_EVIDENCE"] = prefix + ".json";
            using var process = Process.Start(start).ShouldNotBeNull();
            pids.Add(process.Id);
            Task<string> stdout = process.StandardOutput.ReadToEndAsync(), stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
            string output = await stdout + "\n" + await stderr;
            await File.WriteAllTextAsync(prefix + ".log", output, TestContext.Current.CancellationToken);
            process.ExitCode.ShouldBe(phase == "resume" ? 0 : 73, output);
            var proof = JsonNode.Parse(await File.ReadAllTextAsync(prefix + ".json", TestContext.Current.CancellationToken))!;
            proof["pid"]!.GetValue<int>().ShouldBe(process.Id);
            var observed = proof["observations"]!.AsObject();
            if (phase == "stage")
            {
                observed["phase"]!.GetValue<string>().ShouldBe("AfterInstallOperation");
                observed["ordinal"]!.GetValue<int>().ShouldBe(0);
                observed["preparedMarkerObserved"]!.GetValue<bool>().ShouldBeTrue();
                observed["participantInstalled"]!.GetValue<bool>().ShouldBeTrue();
                observed["stageReturned"]!.GetValue<bool>().ShouldBeFalse();
                observed["stageSealed"]!.GetValue<bool>().ShouldBeFalse();
                observed["completedStagePages"]!.GetValue<int>().ShouldBe(0);
            }
            else
            {
                observed["firstOwnerSeeded"]!.GetValue<bool>().ShouldBeTrue();
                observed["secondOwnerCheckpointAbsent"]!.GetValue<bool>().ShouldBeTrue();
                observed["handoffFenceHeld"]!.GetValue<bool>().ShouldBeTrue();
                observed[phase == "promote" ? "secondOwnerEpochInstalled" : "partialHandoffRecovered"]!.GetValue<bool>().ShouldBeTrue();
            }
            if (runtime is null) runtime = proof["runtime"]!.AsArray();
            else proof["runtime"]!.ToJsonString().ShouldBe(runtime.ToJsonString());
            TestContext.Current.TestOutputHelper?.WriteLine($"BoundedIdentity phase={phase} count={count} pid={process.Id} evidence={prefix}.json");
        }
        pids.Distinct().Count().ShouldBe(3);
    }
}
