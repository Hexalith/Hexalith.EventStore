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
            if (runtime is null) runtime = proof["runtime"]!.AsArray();
            else proof["runtime"]!.ToJsonString().ShouldBe(runtime.ToJsonString());
            TestContext.Current.TestOutputHelper?.WriteLine($"BoundedIdentity phase={phase} count={count} pid={process.Id} evidence={prefix}.json");
        }
        pids.Distinct().Count().ShouldBe(3);
    }
}
