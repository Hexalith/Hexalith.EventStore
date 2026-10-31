using System.Diagnostics;

using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

public sealed class RouteCatalogFixtureToolTests
{
    [Theory]
    [InlineData("tcp://remote.invalid:2375", "unix:///var/run/docker.sock")]
    [InlineData("unix:///var/run/docker.sock", "tcp://ignored.invalid:2375")]
    public async Task ConflictingEffectiveContextAndHostRefuseBeforeOwnedContainerMutation(string contextEndpoint, string host)
    {
        string scratch = Path.Combine(Path.GetTempPath(), "catalog-docker-negative-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(scratch);
        try
        {
            string tool = await FakeDockerAsync(scratch, contextEndpoint);
            await Should.ThrowAsync<InvalidOperationException>(() => RouteCatalogDocker.CaptureAsync(tool,
                new Dictionary<string, string?> { ["DOCKER_CONTEXT"] = "selected", ["DOCKER_HOST"] = host }));
            (await File.ReadAllLinesAsync(Path.Combine(scratch, "calls"))).ShouldBe(["context inspect selected"]);
        }
        finally { Directory.Delete(scratch, true); }
    }

    [Fact]
    public async Task CapturedLocalEndpointPinsExecutionAndCleanupDespiteLaterSelectionChanges()
    {
        string scratch = Path.Combine(Path.GetTempPath(), "catalog-docker-pinned-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(scratch);
        try
        {
            string tool = await FakeDockerAsync(scratch, "unix:///fixture/local.sock");
            var selection = new Dictionary<string, string?> { ["DOCKER_CONTEXT"] = "local" };
            RouteCatalogDocker docker = await RouteCatalogDocker.CaptureAsync(tool, selection);
            selection["DOCKER_CONTEXT"] = "remote"; selection["DOCKER_HOST"] = "tcp://remote.invalid:2375";
            await docker.RunAsync(["run", "owned"]); await docker.RunAsync(["rm", "--force", "exact-owned-id"]);
            (await File.ReadAllLinesAsync(Path.Combine(scratch, "calls"))).ShouldBe([
                "context inspect local", "--host unix:///fixture/local.sock run owned", "--host unix:///fixture/local.sock rm --force exact-owned-id"]);
            (await File.ReadAllLinesAsync(Path.Combine(scratch, "selection"))).ShouldBe([
                "unset|unix:///fixture/local.sock", "unset|unix:///fixture/local.sock"]);
        }
        finally { Directory.Delete(scratch, true); }
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("timeout")]
    [InlineData("cancellation")]
    public async Task OwnedInfrastructureSubprocessHasBoundedCancellationAndNormalCompletion(string mode)
    {
        string scratch = Path.Combine(Path.GetTempPath(), "catalog-tool-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(scratch);
        try
        {
            string script = Path.Combine(scratch, "tool.sh"), pid = Path.Combine(scratch, "child.pid");
            await File.WriteAllTextAsync(script, mode != "complete" ? "sleep 60 &\necho $! > \"$1\"\nwait\n" : "printf 'completed'\n");
            var start = new ProcessStartInfo("bash"); start.ArgumentList.Add(script); start.ArgumentList.Add(pid);
            if (mode != "complete")
            {
                using var cancellation = new CancellationTokenSource(mode == "cancellation" ? TimeSpan.FromSeconds(1) : Timeout.InfiniteTimeSpan);
                var elapsed = Stopwatch.StartNew();
                await Should.ThrowAsync<OperationCanceledException>(() => RouteCatalogOwnedProcess.RunAsync(start,
                    mode == "timeout" ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(10), cancellation.Token));
                int child = int.Parse(await File.ReadAllTextAsync(pid));
                string state = "/proc/" + child + "/stat";
                bool exited = false;
                string? lastState = null;
                do
                {
                    lastState = await ReadOwnedChildStateAsync(() => File.ReadAllTextAsync(state));
                    exited = lastState is null or "Z";
                    if (!exited && elapsed.Elapsed < TimeSpan.FromSeconds(7)) await Task.Delay(10);
                }
                while (!exited && elapsed.Elapsed < TimeSpan.FromSeconds(7));
                exited.ShouldBeTrue($"Owned child {child} remained in state {lastState} after cancellation cleanup.");
                elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(7));
            }
            else (await RouteCatalogOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(1))).ShouldBe("completed");
        }
        finally { Directory.Delete(scratch, true); }
    }

    /// <summary>Accepts only process-state read failures which prove that the owned child disappeared.</summary>
    [Theory]
    [InlineData("file")]
    [InlineData("directory")]
    [InlineData("process")]
    public async Task MissingOwnedChildStateIsAccepted(string kind)
    {
        Exception error = kind switch
        {
            "file" => new FileNotFoundException(),
            "directory" => new DirectoryNotFoundException(),
            "process" => new IOException("No such process", 3),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        (await ReadOwnedChildStateAsync(() => Task.FromException<string>(error))).ShouldBeNull();
    }

    /// <summary>Preserves unrelated I/O failures while observing owned child state.</summary>
    [Fact]
    public async Task UnexpectedOwnedChildStateReadFailureIsPreserved()
    {
        var error = new IOException("Unrelated owned fixture read failure", 5);
        IOException actual = await Should.ThrowAsync<IOException>(() => ReadOwnedChildStateAsync(() => Task.FromException<string>(error)));
        actual.ShouldBeSameAs(error);
    }

    private static async Task<string?> ReadOwnedChildStateAsync(Func<Task<string>> read)
    {
        try { return (await read()).Split(' ')[2]; }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        // Linux may return ESRCH after /proc was opened; .NET preserves that errno in HResult.
        catch (IOException error) when (error.HResult == 3) { return null; }
    }

    private static async Task<string> FakeDockerAsync(string scratch, string contextEndpoint)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Owned fixture tooling requires Linux.");
        string tool = Path.Combine(scratch, "docker");
        // All inputs are test-owned, fixed local strings; this executable never calls Docker.
        await File.WriteAllTextAsync(tool, "#!/bin/sh\nbase=$(dirname \"$0\")\nprintf '%s\\n' \"$*\" >> \"$base/calls\"\n"
            + "if [ \"$1\" = context ]; then\n printf '%s\\n' '[{\"Endpoints\":{\"docker\":{\"Host\":\"" + contextEndpoint + "\"}}}]'\n"
            + "else\n printf '%s|%s\\n' \"${DOCKER_CONTEXT-unset}\" \"${DOCKER_HOST-unset}\" >> \"$base/selection\"\nfi\n");
        File.SetUnixFileMode(tool, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return tool;
    }
}
