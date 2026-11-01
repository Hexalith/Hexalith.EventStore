using System.Security.Cryptography;
using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Exercises real process/container failure paths and fail-closed input admission.</summary>
public sealed class OwnershipRegressionTests
{
    /// <summary>Private invocation storage and command records do not export supplied tokens or input.</summary>
    [Fact]
    public async Task CommandSecretsStayPrivateAndUnserializedAsync()
    {
        await using var runtime = new QualificationRuntime();
        if (!OperatingSystem.IsWindows())
            File.GetUnixFileMode(runtime.Root).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        string appApiToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var environment = new Dictionary<string, string>
        {
            ["APP_API_TOKEN"] = appApiToken,
        };
        const string input = "private-input-sentinel-52980";
        await runtime.RunAsync(new("python3", ["-c", "import os,sys; print(sys.stdin.read()); print(os.environ['APP_API_TOKEN'])"],
            runtime.Root, TimeSpan.FromSeconds(10), environment, input));
        Should.Throw<System.ComponentModel.Win32Exception>(() => runtime.Start(new("/missing-p1r-private-binary", [], runtime.Root,
            TimeSpan.FromSeconds(1), environment, input)));
        await runtime.CleanupAsync();
        foreach (string file in Directory.EnumerateFiles(runtime.Root))
        {
            string retained = await File.ReadAllTextAsync(file);
            retained.ShouldNotContain(appApiToken);
            retained.ShouldNotContain(input);
        }
        runtime.CleanupFailures.ShouldBeEmpty();
    }

    /// <summary>An admitted launch cannot finish registration after cleanup's final process scan.</summary>
    [Fact]
    public async Task OverlappingLaunchAndCleanupCloseAdmissionAtomicallyAsync()
    {
        using var admitted = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var cleanupAttempted = new ManualResetEventSlim();
        await using var runtime = new QualificationRuntime(beforeProcessStart: () =>
        {
            admitted.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("The launch overlap fixture was not released.");
        });
        Task<OwnedProcess> launch = Task.Run(() => runtime.Start(new("sleep", ["120"], runtime.Root, TimeSpan.FromSeconds(1))));
        admitted.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        Task cleanup = Task.Run(async () =>
        {
            cleanupAttempted.Set();
            await runtime.CleanupAsync();
        });
        try
        {
            cleanupAttempted.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            await Task.Delay(400, TestContext.Current.CancellationToken);
            cleanup.IsCompleted.ShouldBeFalse();
        }
        finally { release.Set(); }
        OwnedProcess launched = await launch.WaitAsync(TimeSpan.FromSeconds(10));
        await cleanup.WaitAsync(TimeSpan.FromSeconds(10));
        launched.Process.HasExited.ShouldBeTrue();
        Should.Throw<ObjectDisposedException>(() => runtime.Start(new("sleep", ["120"], runtime.Root, TimeSpan.FromSeconds(1))));
        await runtime.CleanupAsync();
        runtime.CleanupFailures.ShouldBeEmpty();
    }

    /// <summary>Replacing a verified container name cannot redirect immutable-ID cleanup to a foreign resource.</summary>
    [Fact]
    public async Task ContainerNameReplacementPreservesForeignIdentityAndRecordsFailureAsync()
    {
        await using var replacementOwner = new QualificationRuntime();
        string replacementCleanupName = replacementOwner.ReserveContainer("replacement-preserved");
        string name = string.Empty;
        string originalId = string.Empty;
        string replacementId = string.Empty;
        var runtime = new QualificationRuntime(beforeContainerRemoval: async verifiedId =>
        {
            originalId = verifiedId;
            await replacementOwner.RequireAsync("docker", ["rename", verifiedId, verifiedId + "-renamed"]);
            replacementId = (await replacementOwner.RequireAsync("docker", ["create", "--name", name,
                "--label", "p1r.owner=" + replacementOwner.Id, PublishedCluster.RedisImage])).Trim();
        });
        name = runtime.ReserveContainer("replace-after-inspect");
        await runtime.RequireAsync("docker", ["create", "--name", name, "--label", "p1r.owner=" + runtime.Id, PublishedCluster.RedisImage]);
        try
        {
            await runtime.CleanupAsync();
            runtime.Commands.ShouldContain(command => command.Command.Arguments.SequenceEqual(new[] { "rm", "-fv", originalId }));
            runtime.Commands.ShouldNotContain(command => command.Command.Arguments.SequenceEqual(new[] { "rm", "-fv", name }));
            using JsonDocument replacement = JsonDocument.Parse(await replacementOwner.RequireAsync("docker", ["inspect", name]));
            replacement.RootElement[0].GetProperty("Id").GetString().ShouldBe(replacementId);
            replacement.RootElement[0].GetProperty("Config").GetProperty("Labels").GetProperty("p1r.owner").GetString().ShouldBe(replacementOwner.Id);
            runtime.CleanupFailures.ShouldContain(failure => failure.Contains("Owned container absence was not established", StringComparison.Ordinal));
            CommandObservation originalAbsent = await replacementOwner.RunAsync(new("docker", ["inspect", originalId], replacementOwner.Root, TimeSpan.FromSeconds(10)));
            originalAbsent.ExitCode.ShouldNotBe(0);
        }
        finally
        {
            await Should.ThrowAsync<InvalidOperationException>(() => runtime.DisposeAsync().AsTask());
            if (!string.IsNullOrEmpty(originalId))
            {
                CommandObservation original = await replacementOwner.RunAsync(new("docker", ["inspect", originalId], replacementOwner.Root, TimeSpan.FromSeconds(10)));
                if (original.ExitCode == 0)
                {
                    using JsonDocument retained = JsonDocument.Parse(original.StandardOutput);
                    retained.RootElement[0].GetProperty("Config").GetProperty("Labels").GetProperty("p1r.owner").GetString().ShouldBe(runtime.Id);
                    await replacementOwner.RequireAsync("docker", ["rm", "-fv", originalId]);
                }
            }
            if (!string.IsNullOrEmpty(replacementId))
            {
                CommandObservation foreign = await replacementOwner.RunAsync(new("docker", ["inspect", replacementId], replacementOwner.Root, TimeSpan.FromSeconds(10)));
                if (foreign.ExitCode == 0)
                {
                    using JsonDocument retained = JsonDocument.Parse(foreign.StandardOutput);
                    retained.RootElement[0].GetProperty("Config").GetProperty("Labels").GetProperty("p1r.owner").GetString().ShouldBe(replacementOwner.Id);
                    await replacementOwner.RequireAsync("docker", ["rename", replacementId, replacementCleanupName]);
                }
            }
        }
    }

    /// <summary>Every role admits only its complete explicit EventStore dependency closure.</summary>
    [Theory]
    [InlineData("Host", "3.128.2", true)]
    [InlineData("Domain", "3.128.2", false)]
    [InlineData("Probe", "3.128.2", false)]
    [InlineData("Host", "3.110.0", false)]
    public async Task ExactRoleSpecificAssetClosureIsRequiredAsync(string role, string version, bool gateway)
    {
        await using var runtime = new QualificationRuntime();
        string path = Path.Combine(runtime.Root, "assets.json");
        string[] packages = gateway ? [.. ConsumerMaterializer.Packages, "Gateway", "Admin.Abstractions"] : ConsumerMaterializer.Packages;
        var libraries = packages.ToDictionary(package => "Hexalith.EventStore." + package + "/" + version, _ => new { type = "package" });
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { libraries }));
        ConsumerMaterializer.ValidateAssets(path, version, source: false, role: role);
        libraries.Add("Hexalith.EventStore.Unexpected/" + version, new { type = "package" });
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { libraries }));
        Should.Throw<InvalidOperationException>(() => ConsumerMaterializer.ValidateAssets(path, version, source: false, role: role))
            .Message.ShouldContain("Unexpected EventStore dependency");
    }

    /// <summary>A failed initial executor capture still writes a nonqualifying summary without sampling a later HEAD.</summary>
    [Fact]
    public async Task MissingExecutorCaptureWritesUnavailableSummaryAsync()
    {
        await using var runtime = new QualificationRuntime();
        const string originalFailure = "Synthetic initial input capture failed before executor-inputs.json was created.";
        var observations = new List<ScenarioObservation> { new("control", "completed", "passed", "compatible", 1, []) };
        await PublishedScenarioTests.WriteSummaryAsync(runtime, observations, originalFailure);
        using JsonDocument summary = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(runtime.Root, "summary.json")));
        summary.RootElement.GetProperty("executor").ValueKind.ShouldBe(JsonValueKind.Null);
        summary.RootElement.GetProperty("executorDiffHash").ValueKind.ShouldBe(JsonValueKind.Null);
        summary.RootElement.GetProperty("executorInputs").ValueKind.ShouldBe(JsonValueKind.Null);
        summary.RootElement.GetProperty("qualified").GetBoolean().ShouldBeFalse();
        summary.RootElement.GetProperty("unavailable").GetInt32().ShouldBe(1);
        ScenarioObservation unavailable = observations.Single(observation => observation.Case == "executor-input-capture");
        unavailable.Execution.ShouldBe("unavailable");
        unavailable.Error.ShouldBe(originalFailure);
    }

    /// <summary>Released ephemeral ports remain invocation-distinct and repeated candidates exhaust a finite budget.</summary>
    [Fact]
    public async Task ReleasedLoopbackPortsStayDistinctAndRepeatedCandidatesAreBoundedAsync()
    {
        await using var runtime = new QualificationRuntime();
        await using var cluster = new PublishedCluster(runtime, []);
        int first = cluster.Port();
        int attempts = 0;
        Should.Throw<InvalidOperationException>(() => cluster.Port(() => { attempts++; return first; }));
        attempts.ShouldBe(128);
        int[] more = Enumerable.Range(0, 64).Select(_ => cluster.Port()).ToArray();
        more.ShouldNotContain(first);
        more.Distinct().Count().ShouldBe(more.Length);
    }

    /// <summary>Noisy finite commands and long-lived children both stop on a measured output overflow.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NoisyOutputIsBoundedNonpassingAndImmediatelyStoppedAsync(bool longLived)
    {
        await using var runtime = new QualificationRuntime();
        var command = new QualificationCommand("python3", ["-c", "import os; b=b'x'*65536\nwhile True: os.write(1,b); os.write(2,b)"],
            runtime.Root, TimeSpan.FromSeconds(15));
        if (longLived)
        {
            OwnedProcess process = runtime.Start(command);
            await process.OverflowCleanup.WaitAsync(TimeSpan.FromSeconds(15));
            process.Process.HasExited.ShouldBeTrue();
        }
        else await Should.ThrowAsync<InvalidOperationException>(() => runtime.RunAsync(command));
        CommandObservation failed = runtime.Commands.ShouldHaveSingleItem();
        failed.ExitCode.ShouldBe(-1);
        failed.OutputTruncated.ShouldBeTrue();
        failed.StandardOutput.Length.ShouldBeLessThanOrEqualTo(BoundedProcessOutput.Limit);
        failed.StandardError.Length.ShouldBeLessThanOrEqualTo(BoundedProcessOutput.Limit + 200);
        failed.StandardError.ShouldContain("bounded capture limit");
        await runtime.CleanupAsync();
        await runtime.CleanupAsync();
        runtime.CleanupFailures.ShouldBeEmpty();
    }

    /// <summary>Concurrent completions retain every command exactly once under unique receipt names.</summary>
    [Fact]
    public async Task ConcurrentCommandsKeepDistinctCompleteReceiptsAsync()
    {
        await using var runtime = new QualificationRuntime();
        await Task.WhenAll(Enumerable.Range(0, 24).Select(index => runtime.RunAsync(new("python3",
            ["-c", "import sys; print(sys.argv[1])", index.ToString(System.Globalization.CultureInfo.InvariantCulture)], runtime.Root, TimeSpan.FromSeconds(15)))));
        runtime.Commands.Count.ShouldBe(24);
        string[] receipts = Directory.GetFiles(runtime.Root, "command-*.json");
        receipts.Length.ShouldBe(24);
        receipts.Select(path => JsonSerializer.Deserialize<CommandObservation>(File.ReadAllText(path))!.StandardOutput.Trim())
            .Distinct(StringComparer.Ordinal).Count().ShouldBe(24);
        await runtime.CleanupAsync();
        runtime.CleanupFailures.ShouldBeEmpty();
    }

    /// <summary>An early-exiting stdin reader retains its failed receipt and immediately removes a detached child.</summary>
    [Fact]
    public async Task BrokenStandardInputIsRecordedAndDescendantsAreStoppedAsync()
    {
        await using var runtime = new QualificationRuntime();
        await Should.ThrowAsync<IOException>(() => runtime.RunAsync(new("python3", ["-c",
            "import os,subprocess; subprocess.Popen(['sleep','120'],start_new_session=True,stdin=subprocess.DEVNULL,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL); os.close(0); os._exit(0)"],
            runtime.Root, TimeSpan.FromSeconds(15), Input: new string('x', 2 * 1024 * 1024))));
        runtime.Commands.ShouldHaveSingleItem().ExitCode.ShouldBe(-1);
        runtime.Commands[0].StandardError.ShouldContain("standard-input delivery failed");
        await runtime.CleanupAsync();
        await runtime.CleanupAsync();
        runtime.CleanupFailures.ShouldBeEmpty();
    }

    /// <summary>The selected SDK must satisfy the exact copied latest-patch policy.</summary>
    [Theory]
    [InlineData("10.0.403", true)]
    [InlineData("10.0.401", false)]
    [InlineData("10.0.500", false)]
    [InlineData("11.0.402", false)]
    public void ExplicitSdkPolicyRefusesDrift(string selected, bool allowed)
    {
        byte[] policy = "{\"sdk\":{\"version\":\"10.0.402\",\"rollForward\":\"latestPatch\"}}"u8.ToArray();
        if (allowed) ConsumerMaterializer.ValidateSdkPolicy(policy, selected);
        else Should.Throw<InvalidOperationException>(() => ConsumerMaterializer.ValidateSdkPolicy(policy, selected));
    }
    /// <summary>Foreign container environment and command values are absent from every retained inventory response.</summary>
    [Fact]
    public async Task ForeignContainerDiagnosticsContainOnlyIdentityStateAndOwnerAsync()
    {
        await using var sentinelOwner = new QualificationRuntime();
        string name = sentinelOwner.ReserveContainer("private-diagnostics-sentinel");
        await sentinelOwner.RequireAsync("docker", ["create", "--name", name, "--label", "p1r.owner=" + sentinelOwner.Id,
            "--env", "P1R_PRIVATE=private-env-marker", "--entrypoint", "private-command-marker", PublishedCluster.RedisImage]);
        await using var runtime = new QualificationRuntime();
        await runtime.CaptureSharedContainersAsync();
        CommandObservation inspection = runtime.Commands.Single(observation => observation.Command.Arguments.Contains(name, StringComparer.Ordinal)
            || observation.StandardOutput.Contains("\"Name\":\"/" + name + "\"", StringComparison.Ordinal));
        inspection.Command.Arguments.ShouldContain("--format");
        inspection.StandardOutput.ShouldNotContain("private-env-marker");
        inspection.StandardOutput.ShouldNotContain("private-command-marker");
        using JsonDocument json = JsonDocument.Parse(inspection.StandardOutput);
        JsonElement item = json.RootElement[0];
        item.EnumerateObject().Select(property => property.Name).Order().ShouldBe(new[] { "Config", "Id", "Image", "Name", "State" });
        item.GetProperty("Config").EnumerateObject().Select(property => property.Name).Order().ShouldBe(new[] { "Image", "Labels" });
        item.GetProperty("Config").GetProperty("Labels").EnumerateObject().Select(property => property.Name).ShouldBe(new[] { "p1r.owner" });
        item.GetProperty("Config").GetProperty("Labels").GetProperty("p1r.owner").GetString().ShouldBe(sentinelOwner.Id);
        await runtime.CleanupAsync();
        await runtime.CleanupAsync();
        runtime.CleanupFailures.ShouldBeEmpty();
    }

    /// <summary>The real provider creates a reserved labeled container before its owned launcher is interrupted.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualContainerCreationLifecycleIsInterruptedAsync(bool cancel)
    {
        await using var runtime = new QualificationRuntime();
        var assertions = new MeasuredAssertions();
        await ContainerCreationDrill.ExecuteAsync(runtime, cancel, assertions);
        assertions.Count.ShouldBe(5);
        assertions.Failures.ShouldBeEmpty();
    }

    /// <summary>Once cleanup closes ownership, a late worker cannot start a new untracked process.</summary>
    [Fact]
    public async Task LateLaunchAfterCleanupIsRejectedAsync()
    {
        await using var runtime = new QualificationRuntime();
        await runtime.CleanupAsync();
        Should.Throw<ObjectDisposedException>(() => runtime.Start(new("sleep", ["120"], runtime.Root, TimeSpan.FromSeconds(1))));
        await runtime.CleanupAsync();
        runtime.CleanupFailures.ShouldBeEmpty();
    }
    /// <summary>A detached descendant retaining redirected pipes cannot escape the finite command boundary.</summary>
    [Fact]
    public async Task DetachedDescendantWithInheritedOutputIsTerminatedAsync()
    {
        await using var runtime = new QualificationRuntime();
        var command = new QualificationCommand("python3", ["-c", "import os,subprocess; subprocess.Popen(['python3','-c','import time; time.sleep(120)'],start_new_session=True)"],
            runtime.Root, TimeSpan.FromSeconds(10));
        await Should.ThrowAsync<InvalidOperationException>(() => runtime.RunAsync(command));
        runtime.Commands.ShouldHaveSingleItem().ExitCode.ShouldBe(-1);
        runtime.Commands[0].StandardError.ShouldContain("Detached descendants retained command output handles");
        runtime.Commands[0].OutputHash.Length.ShouldBe(64);
        await runtime.CleanupAsync();
        await runtime.CleanupAsync();
        runtime.CleanupFailures.ShouldBeEmpty();
    }

    /// <summary>A real child that never reads a pipe cannot make input delivery exceed the command deadline.</summary>
    [Fact]
    public async Task BlockedStandardInputIsBoundedAndRecordedAsync()
    {
        await using var runtime = new QualificationRuntime();
        DateTimeOffset began = DateTimeOffset.UtcNow;
        await Should.ThrowAsync<TimeoutException>(() => runtime.RunAsync(new("python3", ["-c", "import time; time.sleep(120)"],
            runtime.Root, TimeSpan.FromMilliseconds(250), Input: new string('x', 2 * 1024 * 1024))));
        (DateTimeOffset.UtcNow - began).ShouldBeLessThan(TimeSpan.FromSeconds(20));
        runtime.Commands.ShouldHaveSingleItem().ExitCode.ShouldBe(-1);
        runtime.Commands[0].StandardError.ShouldContain("input/execution");
        await runtime.CleanupAsync();
        await runtime.CleanupAsync();
        runtime.CleanupFailures.ShouldBeEmpty();
    }

    /// <summary>Caller-provided environment values cannot replace authoritative inherited ownership markers.</summary>
    [Fact]
    public async Task OwnershipMarkersOverrideCallerEnvironmentAsync()
    {
        await using var runtime = new QualificationRuntime();
        CommandObservation observed = await runtime.RunAsync(new("python3", ["-c", "import os; print(os.environ['P1R_QUALIFICATION_OWNER']); print(os.environ['P1R_COMMAND_OWNER'])"],
            runtime.Root, TimeSpan.FromSeconds(5), new Dictionary<string, string> { ["P1R_QUALIFICATION_OWNER"] = "foreign", ["P1R_COMMAND_OWNER"] = "foreign" }));
        observed.StandardOutput.Split('\n')[0].Trim().ShouldBe(runtime.Id);
        observed.StandardOutput.ShouldNotContain("foreign");
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
        Func<Task> interrupted = () => runtime.RunAsync(new("python3", ["-c", "import subprocess,time; subprocess.Popen(['sleep','120'],start_new_session=True); time.sleep(120)"],
            runtime.Root, cancellation ? TimeSpan.FromSeconds(20) : TimeSpan.FromMilliseconds(200)), cancel.Token);
        if (cancellation) await Should.ThrowAsync<OperationCanceledException>(interrupted);
        else await Should.ThrowAsync<TimeoutException>(interrupted);
        runtime.Commands.Single().Interruption.ShouldBe(cancellation ? "cancelled" : "timeout");
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
        var runtime = new QualificationRuntime();
        await runtime.CaptureSharedContainersAsync();
        string owned = runtime.ReserveContainer("cleanup-after-preservation-failure");
        await runtime.RequireAsync("docker", ["create", "--name", owned, "--label", "p1r.owner=" + runtime.Id, PublishedCluster.RedisImage]);
        await sentinelOwner.RequireAsync("docker", ["stop", sentinel]);
        await runtime.CleanupAsync();
        runtime.CleanupFailures.ShouldContain(failure => failure.Contains("Shared container identity/state changed", StringComparison.Ordinal));
        CommandObservation inspect = await runtime.RunAsync(new("docker", ["inspect", owned], runtime.Root, TimeSpan.FromSeconds(10)));
        inspect.StandardError.ToLowerInvariant().ShouldContain("no such object:");
        await Should.ThrowAsync<InvalidOperationException>(() => runtime.DisposeAsync().AsTask());
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

    /// <summary>A failed invariant cannot leave a compatible claim on its case observation.</summary>
    [Fact]
    public async Task FailedInvariantDowngradesCompatibilityAsync()
    {
        var cases = new List<ScenarioObservation>();
        await PublishedScenarioTests.ObserveAsync(cases, "boundary", "failed-invariant", assertions =>
        {
            assertions.Check(false, "measured-failure");
            return Task.FromResult("compatible");
        });
        cases.ShouldHaveSingleItem().Execution.ShouldBe("failed");
        cases[0].Compatibility.ShouldBe("unverified");
        cases[0].Assertions.ShouldBe(1);
        cases[0].Passing.ShouldBeFalse();
    }

    /// <summary>Physically identified DLL observations with a wrong source, version or configuration refuse admission.</summary>
    [Theory]
    [InlineData("source", "loaded-source-matches-archive-source")]
    [InlineData("version", "loaded-version")]
    [InlineData("configuration", "loaded-configuration")]
    [InlineData("archive-bytes", "loaded-bytes-from-independently-signature-verified-nupkg-entry")]
    [InlineData("duplicate-name", "distinct-exact-required-sdk-assembly-set")]
    [InlineData("omitted-name", "distinct-exact-required-sdk-assembly-set")]
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
        if (mismatch == "duplicate-name") identities = Enumerable.Repeat(identities[0], 5).ToList();
        if (mismatch == "omitted-name") identities.RemoveAt(identities.Count - 1);
        var build = new ConsumerBuild("Probe", ConsumerMaterializer.SelectedVersion, "Release", Path.Combine(runtime.Root, "Probe.csproj"),
            Path.Combine(output, "Probe.dll"), Path.Combine(runtime.Root, "packages"), "assets", "lock", null, sources,
            VerifiedAssemblyHashes: sources.Keys.ToDictionary(name => name, _ => mismatch == "archive-bytes" ? new string('0', 64)
                : QualificationRuntime.Hash(File.ReadAllBytes(typeof(OwnershipRegressionTests).Assembly.Location))));
        var assertions = new MeasuredAssertions();
        PublishedScenarioTests.ValidateIdentity(build, JsonSerializer.SerializeToElement(new { identities }), assertions);
        assertions.Failures.ShouldContain(expectedFailure);
    }
}
