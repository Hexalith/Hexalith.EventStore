using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Exercises real process/container failure paths and fail-closed input admission.</summary>
public sealed class OwnershipRegressionTests
{
    /// <summary>A detached descendant retaining redirected pipes cannot escape the finite command boundary.</summary>
    [Fact]
    public async Task DetachedDescendantWithInheritedOutputIsTerminatedAsync()
    {
        await using var runtime = new QualificationRuntime();
        var command = new QualificationCommand("python3", ["-c", "import os,subprocess; subprocess.Popen(['python3','-c','import time; time.sleep(120)'],start_new_session=True)"],
            runtime.Root, TimeSpan.FromSeconds(10));
        await Should.ThrowAsync<InvalidOperationException>(() => runtime.RunAsync(command));
        await runtime.CleanupAsync();
        await runtime.CleanupAsync();
        runtime.CleanupFailures.ShouldBeEmpty();
    }

    /// <summary>Timeout and cancellation both run repeated exact-owned cleanup.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterruptedCommandRemovesOwnedDescendantsAsync(bool cancellation)
    {
        await using var runtime = new QualificationRuntime();
        using var cancel = new CancellationTokenSource();
        if (cancellation) cancel.CancelAfter(TimeSpan.FromMilliseconds(200));
        await Should.ThrowAsync<TimeoutException>(() => runtime.RunAsync(new("python3", ["-c", "import subprocess,time; subprocess.Popen(['sleep','120'],start_new_session=True); time.sleep(120)"],
            runtime.Root, cancellation ? TimeSpan.FromSeconds(20) : TimeSpan.FromMilliseconds(200)), cancel.Token));
        await runtime.CleanupAsync();
        await runtime.CleanupAsync();
        runtime.Commands.ShouldHaveSingleItem().ExitCode.ShouldBe(-1);
        runtime.CleanupFailures.ShouldBeEmpty();
    }

    /// <summary>A startup exception leaves already-created resources eligible for complete cleanup.</summary>
    [Fact]
    public async Task StartupFailureStillDestroysOwnedSensitiveScratchAsync()
    {
        await using var runtime = new QualificationRuntime();
        string secret = Path.Combine(runtime.Root, "disposable.rdb");
        await File.WriteAllTextAsync(secret, "synthetic-dump");
        runtime.RegisterSensitivePath(secret);
        Should.Throw<System.ComponentModel.Win32Exception>(() => runtime.Start(new("/missing-p1r-binary", [], runtime.Root, TimeSpan.FromSeconds(1))));
        await runtime.CleanupAsync();
        await runtime.CleanupAsync();
        File.Exists(secret).ShouldBeFalse();
        runtime.CleanupFailures.ShouldBeEmpty();
    }

    /// <summary>A container created before cancellation is discovered by its preregistered name and exact label.</summary>
    [Fact]
    public async Task InterruptedContainerCreationAndRepeatedCleanupProveAbsenceAsync()
    {
        await using var runtime = new QualificationRuntime();
        await runtime.CaptureSharedContainersAsync();
        string name = runtime.ReserveContainer("interrupted");
        await runtime.RequireAsync("docker", ["create", "--name", name, "--label", "p1r.owner=" + runtime.Id, PublishedCluster.RedisImage]);
        await runtime.CleanupAsync();
        await runtime.CleanupAsync();
        runtime.CleanupFailures.ShouldBeEmpty();
        CommandObservation inspect = await runtime.RunAsync(new("docker", ["inspect", name], runtime.Root, TimeSpan.FromSeconds(10)));
        inspect.ExitCode.ShouldNotBe(0);
        inspect.StandardError.ToLowerInvariant().ShouldContain("no such object:");
    }

    /// <summary>An independently owned sentinel changing state is retained as preservation failure while cleanup continues.</summary>
    [Fact]
    public async Task SharedPreservationFailureIsNonpassingAndDoesNotStopOtherCleanupAsync()
    {
        await using var sentinelOwner = new QualificationRuntime();
        string sentinel = sentinelOwner.ReserveContainer("sentinel");
        await sentinelOwner.RequireAsync("docker", ["run", "-d", "--name", sentinel, "--label", "p1r.owner=" + sentinelOwner.Id, PublishedCluster.RedisImage]);
        await using var runtime = new QualificationRuntime();
        await runtime.CaptureSharedContainersAsync();
        string owned = runtime.ReserveContainer("cleanup-after-preservation-failure");
        await runtime.RequireAsync("docker", ["create", "--name", owned, "--label", "p1r.owner=" + runtime.Id, PublishedCluster.RedisImage]);
        await sentinelOwner.RequireAsync("docker", ["stop", sentinel]);
        await runtime.CleanupAsync();
        runtime.CleanupFailures.ShouldContain(failure => failure.Contains("Shared container identity/state changed", StringComparison.Ordinal));
        CommandObservation inspect = await runtime.RunAsync(new("docker", ["inspect", owned], runtime.Root, TimeSpan.FromSeconds(10)));
        inspect.StandardError.ToLowerInvariant().ShouldContain("no such object:");
    }

    /// <summary>Malformed, missing and duplicated process results cannot become measured assertions.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("P1R_RESULT invalid")]
    [InlineData("P1R_RESULT {}\nP1R_RESULT {}")]
    public void MalformedProbeOutputsRefuse(string value)
        => Should.Throw<Exception>(() => PublishedScenarioTests.ParseProbeOutput(value));

    /// <summary>Package graphs refuse source substitution and wrong versions before execution.</summary>
    [Theory]
    [InlineData("project", "3.128.2")]
    [InlineData("package", "3.70.1")]
    public async Task UnexpectedGraphRefusesBeforePackageExecutionAsync(string type, string version)
    {
        await using var runtime = new QualificationRuntime();
        string path = Path.Combine(runtime.Root, "assets.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { libraries = new Dictionary<string, object>
        {
            ["Hexalith.EventStore.Client/" + version] = new { type },
        } }));
        Should.Throw<InvalidOperationException>(() => ConsumerMaterializer.ValidateAssets(path, "3.128.2", source: false));
    }

    /// <summary>Missing, failed, incompatible and zero-assertion observations never qualify.</summary>
    [Fact]
    public void ExecutionAndCompatibilityRemainIndependent()
    {
        new ScenarioObservation("control", "incompatible", "passed", "incompatible", 4, []).Passing.ShouldBeFalse();
        new ScenarioObservation("control", "missing", "unavailable", "unverified", 0, []).Passing.ShouldBeFalse();
        new ScenarioObservation("control", "zero", "passed", "compatible", 0, []).Passing.ShouldBeFalse();
        new ScenarioObservation("control", "exception", "failed", "compatible", 4, []).Passing.ShouldBeFalse();
    }

    /// <summary>Physically identified DLL observations with a wrong source, version or configuration refuse admission.</summary>
    [Theory]
    [InlineData("source", "loaded-source-matches-archive-source")]
    [InlineData("version", "loaded-version")]
    [InlineData("configuration", "loaded-configuration")]
    public async Task WrongLoadedIdentityIsNonpassingAsync(string mismatch, string expectedFailure)
    {
        await using var runtime = new QualificationRuntime();
        string output = Path.Combine(runtime.Root, "bin");
        Directory.CreateDirectory(output);
        var sources = ConsumerMaterializer.Packages.ToDictionary(name => "Hexalith.EventStore." + name, _ => ConsumerMaterializer.SelectedSource);
        var identities = new List<object>();
        foreach (string name in sources.Keys)
        {
            string path = Path.Combine(output, name + ".dll");
            byte[] bytes = await File.ReadAllBytesAsync(typeof(OwnershipRegressionTests).Assembly.Location);
            await File.WriteAllBytesAsync(path, bytes);
            string cache = Path.Combine(runtime.Root, "packages", name.ToLowerInvariant(), ConsumerMaterializer.SelectedVersion, "lib", "net10.0");
            Directory.CreateDirectory(cache);
            await File.WriteAllBytesAsync(Path.Combine(cache, name + ".dll"), bytes);
            identities.Add(new { name, path, sha256 = QualificationRuntime.Hash(bytes),
                configuration = mismatch == "configuration" ? "Debug" : "Release",
                informationalVersion = (mismatch == "version" ? "3.70.1" : ConsumerMaterializer.SelectedVersion)
                    + "+" + (mismatch == "source" ? new string('0', 40) : ConsumerMaterializer.SelectedSource) });
        }
        var build = new ConsumerBuild("Probe", ConsumerMaterializer.SelectedVersion, "Release", Path.Combine(runtime.Root, "Probe.csproj"),
            Path.Combine(output, "Probe.dll"), Path.Combine(runtime.Root, "packages"), "assets", "lock", null, sources);
        var assertions = new MeasuredAssertions();
        PublishedScenarioTests.ValidateIdentity(build, JsonSerializer.SerializeToElement(new { identities }), assertions);
        assertions.Failures.ShouldContain(expectedFailure);
    }
}
