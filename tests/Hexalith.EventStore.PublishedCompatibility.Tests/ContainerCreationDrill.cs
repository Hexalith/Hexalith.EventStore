using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Interrupts an actual owned provider creation before its launcher can return the creation result.</summary>
internal static class ContainerCreationDrill
{
    /// <summary>The exact actual owned provider-launch program; validator binds its command capture to this producer.</summary>
    internal const string LaunchScript = "import subprocess,pathlib,time,sys,json; result=subprocess.run(['docker','create','--name',sys.argv[1],'--label','p1r.owner='+sys.argv[2],sys.argv[3]],check=True,capture_output=True,text=True); path=pathlib.Path(sys.argv[4]); temporary=path.with_suffix('.tmp'); temporary.write_text(json.dumps({'containerId':result.stdout.strip()})); temporary.replace(path); time.sleep(120); print(result.stdout)";

    /// <summary>Proves provider creation, interrupts the still-running launcher, then verifies owned absence and shared preservation twice.</summary>
    internal static async Task ExecuteAsync(QualificationRuntime runtime, bool cancel, MeasuredAssertions assertions)
    {
        await runtime.CaptureSharedContainersAsync();
        string name = runtime.ReserveContainer("creation-lifecycle");
        string marker = Path.Combine(runtime.Root, "provider-created.json");
        using var cancellation = new CancellationTokenSource();
        Task<CommandObservation> launch = runtime.RunAsync(new("python3", ["-c", LaunchScript, name, runtime.Id, PublishedCluster.RedisImage, marker],
            runtime.Root, cancel ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(8)), cancellation.Token);
        DateTimeOffset markerDeadline = DateTimeOffset.UtcNow.AddSeconds(6);
        while (!File.Exists(marker) && !launch.IsCompleted && DateTimeOffset.UtcNow < markerDeadline)
            await Task.Delay(25, TestContext.Current.CancellationToken);
        bool created = File.Exists(marker);
        assertions.Check(created && !launch.IsCompleted, "provider-created-before-launcher-returned-result");
        if (created)
        {
            using JsonDocument proof = JsonDocument.Parse(await File.ReadAllTextAsync(marker));
            CommandObservation inspected = await runtime.RunAsync(new("docker", ["inspect", name], runtime.Root, TimeSpan.FromSeconds(5)));
            assertions.Record("provider-created-before-interruption", inspected.StandardOutput);
            string image = await runtime.RequireAsync("docker", ["image", "inspect", "--format", QualificationImageIdentity.Format, PublishedCluster.RedisImage], timeout: TimeSpan.FromSeconds(5));
            assertions.Record("provider-local-image", image);
            bool owned = false;
            if (inspected.ExitCode == 0)
            {
                using JsonDocument container = JsonDocument.Parse(inspected.StandardOutput);
                owned = container.RootElement[0].GetProperty("Id").GetString() == proof.RootElement.GetProperty("containerId").GetString()
                    && container.RootElement[0].GetProperty("Config").GetProperty("Labels").GetProperty("p1r.owner").GetString() == runtime.Id
                    && container.RootElement[0].GetProperty("Image").GetString() == QualificationImageIdentity.RequireApprovedEngineId(JsonSerializer.Deserialize<JsonElement>(image));
            }
            assertions.Check(owned, "actual-provider-container-id-and-label-before-interruption");
        }
        if (cancel || !created) cancellation.Cancel();
        bool interrupted = false;
        try { await launch; }
        catch (TimeoutException) { interrupted = true; }
        assertions.Check(interrupted, "actual-container-creation-launcher-interrupted");
        await runtime.CleanupAsync();
        await runtime.CleanupAsync();
        assertions.Check(runtime.CleanupFailures.Count == 0, "creation-lifecycle-repeated-owned-cleanup-shared-preservation");
        CommandObservation absent = await runtime.RunAsync(new("docker", ["inspect", name], runtime.Root, TimeSpan.FromSeconds(5)));
        assertions.Check(absent.ExitCode != 0 && absent.StandardError.Contains("no such object:", StringComparison.OrdinalIgnoreCase), "creation-lifecycle-container-proven-absent");
    }
}
