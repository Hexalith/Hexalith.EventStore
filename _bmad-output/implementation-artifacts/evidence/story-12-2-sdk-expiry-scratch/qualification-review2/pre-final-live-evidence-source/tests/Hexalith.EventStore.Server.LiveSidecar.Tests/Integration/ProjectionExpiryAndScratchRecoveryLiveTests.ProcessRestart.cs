using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Dapr.Client;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.DomainService;
using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

public sealed partial class ProjectionExpiryAndScratchRecoveryLiveTests
{
    [Theory]
    [InlineData(false, true, ReadModelBatchPhase.AfterInstallOperation, 0)]
    [InlineData(true, false, ReadModelBatchPhase.BeforeCommit, -1)]
    [InlineData(false, true, ReadModelBatchPhase.MarkerPrepared, -1)]
    [InlineData(true, false, ReadModelBatchPhase.MarkerPrepared, -1)]
    [InlineData(false, false, ReadModelBatchPhase.BeforeInstallOperation, 0)]
    [InlineData(true, true, ReadModelBatchPhase.BeforeInstallOperation, 0)]
    public async Task PendingNamedOwnerScratchRecoversAfterOwningOsProcessExits(bool populated, bool retryFirst,
        ReadModelBatchPhase stop, int ordinal)
    {
        string tenant = "scratch-process-" + Guid.NewGuid().ToString("N");
        int originalPid = await RunScratchProcessAsync("persist", tenant, populated, retryFirst, stop, ordinal);
        using DaprClient client = Client();
        var store = new DaprReadModelStore(client);
        var handler = new LivePartitionedScratchHandler();
        SharedProjectionScope scope = handler.CreateScope(tenant);
        DomainSharedProjectionRebuildIdentity identity = NamedIdentity(tenant, "process-interrupted");
        string session = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        string scratch = DomainSharedProjectionRebuildDispatcher.PartitionChunkKey(session, 0, 0);
        byte[] scratchRaw = (byte[])(await _redis.GetDatabase().HashGetAsync(Key(scratch), "data"))!;
        byte[] fenceRaw = (byte[])(await _redis.GetDatabase().HashGetAsync(Key(scope.StateKey), "data"))!;
        if (stop is ReadModelBatchPhase.MarkerPrepared or ReadModelBatchPhase.BeforeInstallOperation)
        {
            scratchRaw.ShouldBeNull(); ReadModelBatchEnvelope.IsEnvelope(fenceRaw).ShouldBeFalse();
            await AssertPreparedOwnerManifestAsync(client, scope, identity);
        }
        else (ReadModelBatchEnvelope.IsEnvelope(scratchRaw) || ReadModelBatchEnvelope.IsEnvelope(fenceRaw))
            .ShouldBeTrue("pending original owner-scratch bytes must remain after the owning OS process has exited");
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        ((await coordinator.ReadAsync<JsonObject>(scope, "row:owner-a")).Value?["count"]?.GetValue<int>()).ShouldBe(populated ? 1 : null);
        int resumedPid = await RunScratchProcessAsync("resume", tenant, populated, retryFirst, stop, ordinal);
        resumedPid.ShouldNotBe(originalPid);
        resumedPid.ShouldNotBe(Environment.ProcessId);
        originalPid.ShouldNotBe(Environment.ProcessId);
        (await _redis.GetDatabase().KeyExistsAsync(Key(scratch))).ShouldBeFalse();
        (await _redis.GetDatabase().KeyExistsAsync(Key(DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(session, 0)))).ShouldBeFalse();
        byte[] persisted = (byte[])(await _redis.GetDatabase().HashGetAsync(Key(scope.StateKey), "data"))!;
        JsonSerializer.Deserialize<SharedProjectionEpochState>(persisted, JsonSerializerOptions.Web)!.Phase.ShouldBe(SharedProjectionEpochPhase.Open);
        (await coordinator.ReadAsync<JsonObject>(scope, "row:owner-b")).Value!["count"]!.GetValue<int>().ShouldBe(9);
        TestContext.Current.TestOutputHelper!.WriteLine($"owning-process-pid={originalPid}; recovery-process-pid={resumedPid}; controller-pid={Environment.ProcessId}; original-process-exited-before-recovery=true; populated={populated}; retry-first={retryFirst}; real-pending-evidence-after-exit=true; private-cleaned=true; persisted-epoch=Open; ordinary-owners=2; fresh-named-rebuild=true; same-fixture-sidecar=true");
    }

    private async Task<int> RunScratchProcessAsync(string phase, string tenant, bool populated, bool retryFirst, ReadModelBatchPhase stop, int ordinal)
    {
        string directory = Environment.GetEnvironmentVariable("EVENTSTORE_SCRATCH_PROCESS_EVIDENCE_DIR")
            ?? Path.Combine(Path.GetTempPath(), "hexalith-scratch-process-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string prefix = Path.Combine(directory, tenant + "-" + phase);
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (string argument in new[] { typeof(ProjectionScratchProcessWorkerTests).Assembly.Location,
            "-class", typeof(ProjectionScratchProcessWorkerTests).FullName!, "-showliveoutput", "-result-xml", prefix + ".xml" })
            start.ArgumentList.Add(argument);
        var environment = new Dictionary<string, string>
        {
            ["EXPIRY_SCRATCH_PROCESS_PHASE"] = phase,
            ["EXPIRY_SCRATCH_PROCESS_STOP"] = stop.ToString(),
            ["EXPIRY_SCRATCH_PROCESS_ORDINAL"] = ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["EXPIRY_SCRATCH_PROCESS_ENDPOINT"] = fixture.DaprGrpcEndpoint,
            ["EXPIRY_SCRATCH_PROCESS_TENANT"] = tenant,
            ["EXPIRY_SCRATCH_PROCESS_POPULATED"] = populated.ToString(),
            ["EXPIRY_SCRATCH_PROCESS_RETRY"] = retryFirst.ToString(),
        };
        foreach ((string key, string value) in environment) start.Environment[key] = value;
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Cannot launch owner-scratch process.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromMinutes(2)); }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        string output = await stdout; string error = await stderr;
        await File.WriteAllTextAsync(prefix + ".log", output + error);
        var record = new { command = new[] { start.FileName }.Concat(start.ArgumentList), environment, pid = process.Id, exit_code = process.ExitCode, stdout = output, stderr = error };
        await File.WriteAllTextAsync(prefix + ".json", JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.Current.TestOutputHelper!.WriteLine(JsonSerializer.Serialize(record));
        process.ExitCode.ShouldBe(0, output + error);
        XElement assembly = XDocument.Load(prefix + ".xml").Root!.Element("assembly")!;
        assembly.Attribute("total")!.Value.ShouldBe("1");
        assembly.Attribute("passed")!.Value.ShouldBe("1");
        assembly.Attribute("skipped")!.Value.ShouldBe("0");
        return process.Id;
    }
}
