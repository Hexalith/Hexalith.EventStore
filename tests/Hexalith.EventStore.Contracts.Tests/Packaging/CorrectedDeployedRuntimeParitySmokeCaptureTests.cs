using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Executes the Story 3.15 smoke-capture utility against recording and failing command fakes.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class CorrectedDeployedRuntimeParitySmokeCaptureTests
{
    /// <summary>
    /// Skips on a non-Unix host. <see cref="SupportedOSPlatformAttribute"/> is an analyzer hint the
    /// runner does not act on, so without this the shell fakes would simply fail on Windows.
    /// </summary>
    private static void RequireUnixHost()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Assert.Skip("The smoke-capture fakes are POSIX shell scripts and need a Unix host.");
        }
    }

    private const string Amd64Digest =
        "sha256:4d42f969dc5f57e0f9baa927c588346d77c31fd2615793b5d8c12c239585af63";
    private const string Arm64Digest =
        "sha256:ede853318267146a9888574f79e16ea1e51c1f363a35910fe883b5a9d7256f44";

    /// <summary>
    /// Verifies the actual docker and curl argv agree with the retained Production /alive facts.
    /// </summary>
    [Fact]
    public void RecordingFakesObserveTheSelfReportedProductionAliveContract()
    {
        RequireUnixHost();
        string root = FindRepositoryRoot();
        string temporary = Directory.CreateTempSubdirectory("eventstore-story315-smoke-recording-").FullName;
        try
        {
            string fakeBin = Path.Combine(temporary, "bin");
            Directory.CreateDirectory(fakeBin);
            string dockerLog = Path.Combine(temporary, "docker-argv.log");
            string curlLog = Path.Combine(temporary, "curl-argv.log");
            WriteExecutable(Path.Combine(fakeBin, "docker"), DockerFake("success"));
            WriteExecutable(Path.Combine(fakeBin, "curl"), CurlFake("200 0"));

            ProcessResult result = RunCapture(root, temporary, fakeBin, dockerLog, curlLog);

            result.ExitCode.ShouldBe(0, result.Error);
            JsonObject summary = LoadSummary(temporary);
            summary["environment"]!.GetValue<string>().ShouldBe("Production");
            summary["endpoint"]!.GetValue<string>().ShouldBe("/alive");
            summary["timeout_seconds"]!.GetValue<int>().ShouldBe(180);
            summary["result"]!.GetValue<string>().ShouldBe("pass");
            summary["platforms"]!.AsArray().Count.ShouldBe(2);
            summary["platforms"]!.AsArray().ShouldAllBe(item =>
                item!["http_status"]!.GetValue<int>() == 200
                && item["redirect_count"]!.GetValue<int>() == 0
                && item["outcome"]!.GetValue<string>() == "pass");

            string[] dockerArguments = File.ReadAllLines(dockerLog);
            dockerArguments.ShouldContain(line =>
                line == $"pull --platform linux/amd64 registry.hexalith.com/eventstore@{Amd64Digest}");
            dockerArguments.ShouldContain(line =>
                line == $"pull --platform linux/arm64 registry.hexalith.com/eventstore@{Arm64Digest}");
            dockerArguments.Count(line =>
                line.Contains("--env ASPNETCORE_ENVIRONMENT=Production", StringComparison.Ordinal)
                && line.Contains("--env DOTNET_ENVIRONMENT=Production", StringComparison.Ordinal)
                && line.Contains("--env ASPNETCORE_URLS=http://+:8080", StringComparison.Ordinal)
                && line.Contains("--publish 127.0.0.1::8080", StringComparison.Ordinal))
                .ShouldBe(2);

            string[] curlArguments = File.ReadAllLines(curlLog);
            curlArguments.Length.ShouldBe(2);
            foreach (string line in curlArguments)
            {
                line.ShouldContain("--output /dev/null");
                line.ShouldContain("--write-out %{http_code} %{num_redirects}");
                // Assert the operand, not just the flag: an unbounded or oversized budget would
                // still satisfy a presence-only check.
                Match maxTime = Regex.Match(line, @"--max-time (\d+\.\d+)");
                maxTime.Success.ShouldBeTrue(line);
                double budget = double.Parse(maxTime.Groups[1].Value, CultureInfo.InvariantCulture);
                budget.ShouldBeGreaterThan(0.0);
                budget.ShouldBeLessThanOrEqualTo(5.0);
                line.ShouldContain("--noproxy *");
                line.ShouldContain("http://127.0.0.1:45678/alive");
                line.ShouldNotContain("--location");
            }
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies command failure, malformed curl output, and cleanup command/OSError paths all
    /// retain two platform logs plus the aggregate failure summary.
    /// </summary>
    /// <param name="mode">Failure injected by the command fakes.</param>
    [Theory]
    [InlineData("pull-failure")]
    [InlineData("malformed-curl")]
    [InlineData("cleanup-command-failure")]
    [InlineData("cleanup-oserror")]
    public void FailurePathsAlwaysRetainRecordsAndSummary(string mode)
    {
        RequireUnixHost();
        string root = FindRepositoryRoot();
        string temporary = Directory.CreateTempSubdirectory("eventstore-story315-smoke-failure-").FullName;
        try
        {
            string fakeBin = Path.Combine(temporary, "bin");
            Directory.CreateDirectory(fakeBin);
            string dockerLog = Path.Combine(temporary, "docker-argv.log");
            string curlLog = Path.Combine(temporary, "curl-argv.log");
            WriteExecutable(Path.Combine(fakeBin, "docker"), DockerFake(mode));
            WriteExecutable(
                Path.Combine(fakeBin, "curl"),
                CurlFake(mode == "malformed-curl" ? "not-a-curl-write-out" : "200 0"));

            ProcessResult result = RunCapture(
                root,
                temporary,
                fakeBin,
                dockerLog,
                curlLog,
                isolateFakePath: mode == "cleanup-oserror");

            result.ExitCode.ShouldBe(1, result.Error);
            result.Error.ShouldNotContain("Traceback");
            JsonObject summary = LoadSummary(temporary);
            summary["result"]!.GetValue<string>().ShouldBe("failure");
            summary["exit_code"]!.GetValue<int>().ShouldBe(1);
            summary["platforms"]!.AsArray().Count.ShouldBe(2);
            File.Exists(Path.Combine(temporary, "smokes", "smoke-linux-amd64.log")).ShouldBeTrue();
            File.Exists(Path.Combine(temporary, "smokes", "smoke-linux-arm64.log")).ShouldBeTrue();
            summary["platforms"]!.AsArray().ShouldAllBe(item =>
                item!["outcome"]!.GetValue<string>() == "failure");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies cleanup is bounded by its own budget: a hanging fake is timed out promptly and the
    /// failure record is still written.
    /// </summary>
    [Fact]
    public void CleanupTimeoutIsBoundedAndRetained()
    {
        RequireUnixHost();
        string root = FindRepositoryRoot();
        string temporary = Directory.CreateTempSubdirectory("eventstore-story315-smoke-timeout-").FullName;
        try
        {
            string fakeBin = Path.Combine(temporary, "bin");
            Directory.CreateDirectory(fakeBin);
            string dockerLog = Path.Combine(temporary, "docker-argv.log");
            string curlLog = Path.Combine(temporary, "curl-argv.log");
            WriteExecutable(Path.Combine(fakeBin, "docker"), DockerFake("cleanup-timeout"));
            WriteExecutable(Path.Combine(fakeBin, "curl"), CurlFake("200 0"));

            Stopwatch elapsed = Stopwatch.StartNew();
            ProcessResult result = RunCapture(
                root,
                temporary,
                fakeBin,
                dockerLog,
                curlLog,
                timeoutOverride: 0.2,
                cleanupTimeoutOverride: 0.2);
            elapsed.Stop();

            result.ExitCode.ShouldBe(1, result.Error);
            elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
            JsonObject summary = LoadSummary(temporary);
            summary["result"]!.GetValue<string>().ShouldBe("failure");
            summary["platforms"]!.AsArray().Count.ShouldBe(2);
            summary["platforms"]!.AsArray().ShouldAllBe(item =>
                item!["cleanup"]!.GetValue<string>() == "failure"
                && item["outcome"]!.GetValue<string>() == "failure");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies the readiness loop actually polls. Every existing case answered on attempt one, so
    /// replacing the accept predicate with an unconditional break passed the whole suite and the
    /// bounded back-off ran in no test at all -- the poll/retry behaviour this utility exists for
    /// was completely unpinned.
    /// </summary>
    [Fact]
    public void ReadinessLoopPollsUntilTheServiceAnswers()
    {
        RequireUnixHost();
        string root = FindRepositoryRoot();
        string temporary = Directory.CreateTempSubdirectory("eventstore-story315-smoke-retry-").FullName;
        try
        {
            string fakeBin = Path.Combine(temporary, "bin");
            Directory.CreateDirectory(fakeBin);
            string dockerLog = Path.Combine(temporary, "docker-argv.log");
            string curlLog = Path.Combine(temporary, "curl-argv.log");
            WriteExecutable(Path.Combine(fakeBin, "docker"), DockerFake("success"));
            WriteExecutable(
                Path.Combine(fakeBin, "curl"),
                RetryingCurlFake(Path.Combine(temporary, "curl-attempts")));

            ProcessResult result = RunCapture(root, temporary, fakeBin, dockerLog, curlLog);

            result.ExitCode.ShouldBe(0, result.Error);
            JsonObject summary = LoadSummary(temporary);
            summary["result"]!.GetValue<string>().ShouldBe("pass");

            // Three curl invocations per platform: connection-refused, a non-200, then 200.
            summary["platforms"]!.AsArray().Count.ShouldBe(2);
            summary["platforms"]!.AsArray().ShouldAllBe(item =>
                item!["attempts"]!.GetValue<int>() == 3
                && item["http_status"]!.GetValue<int>() == 200
                && item["outcome"]!.GetValue<string>() == "pass");
            File.ReadAllLines(curlLog).Length.ShouldBe(6);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies cleanup is attempted -- not silently skipped -- in the exact failure mode it exists
    /// for. Reusing the already exhausted platform deadline made the budget check raise before
    /// subprocess.run was ever called, so no <c>rm --force</c> reached docker at all while the
    /// record still claimed a timed-out attempt and the container leaked.
    /// </summary>
    [Fact]
    public void CleanupIsAttemptedAfterThePlatformBudgetIsExhausted()
    {
        RequireUnixHost();
        string root = FindRepositoryRoot();
        string temporary = Directory.CreateTempSubdirectory("eventstore-story315-smoke-exhausted-").FullName;
        try
        {
            string fakeBin = Path.Combine(temporary, "bin");
            Directory.CreateDirectory(fakeBin);
            string dockerLog = Path.Combine(temporary, "docker-argv.log");
            string curlLog = Path.Combine(temporary, "curl-argv.log");
            WriteExecutable(Path.Combine(fakeBin, "docker"), DockerFake("success"));

            // Never ready: the readiness loop burns the whole platform budget.
            WriteExecutable(Path.Combine(fakeBin, "curl"), CurlFake("503 0"));

            ProcessResult result = RunCapture(
                root,
                temporary,
                fakeBin,
                dockerLog,
                curlLog,
                timeoutOverride: 1.0);

            result.ExitCode.ShouldBe(1, result.Error);
            result.Error.ShouldNotContain("Traceback");

            string[] dockerArguments = File.ReadAllLines(dockerLog);
            dockerArguments.Count(line => line.StartsWith("rm --force ", StringComparison.Ordinal))
                .ShouldBe(2);

            JsonObject summary = LoadSummary(temporary);
            summary["platforms"]!.AsArray().Count.ShouldBe(2);
            summary["platforms"]!.AsArray().ShouldAllBe(item =>
                item!["cleanup"]!.GetValue<string>() == "pass"
                && item["readiness_result"]!.GetValue<string>() == "failure"
                && item["outcome"]!.GetValue<string>() == "failure");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies cleanup owns only containers successfully created by this capture. A failed
    /// <c>docker run</c> may leave an unrelated same-named container in Docker's namespace; the
    /// producer must not force-remove it merely because it chose that name.
    /// </summary>
    [Fact]
    public void FailedDockerRunNeverRemovesAContainerTheCaptureDidNotCreate()
    {
        RequireUnixHost();
        string root = FindRepositoryRoot();
        string temporary = Directory.CreateTempSubdirectory("eventstore-story315-smoke-run-failure-").FullName;
        try
        {
            string fakeBin = Path.Combine(temporary, "bin");
            Directory.CreateDirectory(fakeBin);
            string dockerLog = Path.Combine(temporary, "docker-argv.log");
            string curlLog = Path.Combine(temporary, "curl-argv.log");
            WriteExecutable(Path.Combine(fakeBin, "docker"), DockerFake("run-failure"));
            WriteExecutable(Path.Combine(fakeBin, "curl"), CurlFake("200 0"));

            ProcessResult result = RunCapture(root, temporary, fakeBin, dockerLog, curlLog);

            result.ExitCode.ShouldBe(1, result.Error);
            result.Error.ShouldNotContain("Traceback");
            File.ReadAllLines(dockerLog)
                .ShouldNotContain(line => line.StartsWith("rm --force ", StringComparison.Ordinal));
            LoadSummary(temporary)["platforms"]!.AsArray().ShouldAllBe(item =>
                item!["cleanup"]!.GetValue<string>() == "pass"
                && item["outcome"]!.GetValue<string>() == "failure");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies log and aggregate-summary write failures use the documented support-safe failure
    /// and rerun guidance instead of escaping as a traceback after runtime capture has completed.
    /// </summary>
    /// <param name="mode">Write failure injected by the Docker fake.</param>
    [Theory]
    [InlineData("log-write-failure")]
    [InlineData("summary-write-failure")]
    public void EvidenceWriteFailuresRemainSupportSafe(string mode)
    {
        RequireUnixHost();
        string root = FindRepositoryRoot();
        string temporary = Directory.CreateTempSubdirectory("eventstore-story315-smoke-write-").FullName;
        try
        {
            string fakeBin = Path.Combine(temporary, "bin");
            Directory.CreateDirectory(fakeBin);
            string dockerLog = Path.Combine(temporary, "docker-argv.log");
            string curlLog = Path.Combine(temporary, "curl-argv.log");
            WriteExecutable(Path.Combine(fakeBin, "docker"), DockerFake(mode));
            WriteExecutable(Path.Combine(fakeBin, "curl"), CurlFake("200 0"));

            ProcessResult result = RunCapture(root, temporary, fakeBin, dockerLog, curlLog);

            result.ExitCode.ShouldBe(1, result.Error);
            result.Error.ShouldContain("retained smoke evidence could not be written safely");
            result.Error.ShouldContain("rerun: ");
            result.Error.ShouldNotContain("Traceback");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a symlinked smoke output directory is rejected even with <c>--force</c>, so producer
    /// writes cannot escape the packet root through an operator-controlled link.
    /// </summary>
    [Fact]
    public void SymlinkedSmokeOutputDirectoryFailsBeforeAnyExternalCommand()
    {
        RequireUnixHost();
        string root = FindRepositoryRoot();
        string temporary = Directory.CreateTempSubdirectory("eventstore-story315-smoke-link-").FullName;
        string outside = Directory.CreateTempSubdirectory("eventstore-story315-smoke-outside-").FullName;
        try
        {
            string fakeBin = Path.Combine(temporary, "bin");
            Directory.CreateDirectory(fakeBin);
            string dockerLog = Path.Combine(temporary, "docker-argv.log");
            string curlLog = Path.Combine(temporary, "curl-argv.log");
            WriteExecutable(Path.Combine(fakeBin, "docker"), DockerFake("success"));
            WriteExecutable(Path.Combine(fakeBin, "curl"), CurlFake("200 0"));
            Directory.CreateSymbolicLink(Path.Combine(temporary, "smokes"), outside);

            ProcessResult result = RunCapture(
                root,
                temporary,
                fakeBin,
                dockerLog,
                curlLog,
                force: true);

            result.ExitCode.ShouldBe(2, result.Error);
            result.Error.ShouldContain("not a regular smoke output directory");
            result.Error.ShouldNotContain("Traceback");
            File.Exists(dockerLog).ShouldBeFalse();
            Directory.EnumerateFileSystemEntries(outside).ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    /// <summary>
    /// Verifies the capture refuses to overwrite an already populated packet <c>smokes/</c>
    /// directory unless the operator opts in. Running it against a live packet root previously
    /// replaced the three hash-bound smoke files with failure records, recoverable only through git.
    /// </summary>
    [Fact]
    public void PopulatedSmokeDirectoryIsRefusedWithoutForce()
    {
        RequireUnixHost();
        string root = FindRepositoryRoot();
        string temporary = Directory.CreateTempSubdirectory("eventstore-story315-smoke-guard-").FullName;
        try
        {
            string fakeBin = Path.Combine(temporary, "bin");
            Directory.CreateDirectory(fakeBin);
            string dockerLog = Path.Combine(temporary, "docker-argv.log");
            string curlLog = Path.Combine(temporary, "curl-argv.log");
            WriteExecutable(Path.Combine(fakeBin, "docker"), DockerFake("success"));
            WriteExecutable(Path.Combine(fakeBin, "curl"), CurlFake("200 0"));

            string smokes = Path.Combine(temporary, "smokes");
            Directory.CreateDirectory(smokes);
            string retained = Path.Combine(smokes, "smoke-results.json");
            File.WriteAllText(retained, "retained evidence\n");

            ProcessResult refused = RunCapture(root, temporary, fakeBin, dockerLog, curlLog);

            // Exit 2, distinct from the exit 1 a genuine smoke failure produces, so a caller can
            // tell "I would have destroyed retained evidence" from "the runtime did not answer".
            refused.ExitCode.ShouldBe(2, refused.Error);
            refused.Error.ShouldContain("already holds retained smoke evidence");

            // The rerun text must not tell the operator to re-run the capture, which this very
            // guard would refuse again; it has to name --force or an empty packet root.
            refused.Error.ShouldContain("rerun: ");
            refused.Error.ShouldContain("--force");
            refused.Error.ShouldContain("empty packet root");
            File.ReadAllText(retained).ShouldBe("retained evidence\n");
            File.Exists(dockerLog).ShouldBeFalse();

            ProcessResult forced = RunCapture(root, temporary, fakeBin, dockerLog, curlLog, force: true);

            forced.ExitCode.ShouldBe(0, forced.Error);
            LoadSummary(temporary)["result"]!.GetValue<string>().ShouldBe("pass");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    private static ProcessResult RunCapture(
        string root,
        string packetRoot,
        string fakeBin,
        string dockerLog,
        string curlLog,
        double? timeoutOverride = null,
        double? cleanupTimeoutOverride = null,
        bool isolateFakePath = false,
        bool force = false)
    {
        string script = Path.Combine(root, "tools", "capture-corrected-deployed-runtime-parity-smokes.py");
        ProcessStartInfo startInfo = new("python3")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        if (timeoutOverride is null && cleanupTimeoutOverride is null)
        {
            startInfo.ArgumentList.Add(script);
        }
        else
        {
            // The platform budget and the cleanup budget are independent, so a test that shortens
            // one must be able to leave the other alone: sharing them is exactly the defect that
            // made cleanup get skipped instead of bounded.
            string overrides = string.Empty;
            if (timeoutOverride is not null)
            {
                overrides +=
                    $"m.TIMEOUT_SECONDS={timeoutOverride.Value.ToString(CultureInfo.InvariantCulture)};";
            }

            if (cleanupTimeoutOverride is not null)
            {
                overrides +=
                    $"m.CLEANUP_TIMEOUT_SECONDS={cleanupTimeoutOverride.Value.ToString(CultureInfo.InvariantCulture)};";
            }

            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(
                "import importlib.util,sys;" +
                "s=importlib.util.spec_from_file_location('story315_smoke',sys.argv[1]);" +
                "m=importlib.util.module_from_spec(s);s.loader.exec_module(m);" +
                overrides +
                "sys.argv=[sys.argv[1]]+sys.argv[2:];raise SystemExit(m.main())");
            startInfo.ArgumentList.Add(script);
        }

        startInfo.ArgumentList.Add(packetRoot);
        if (force)
        {
            startInfo.ArgumentList.Add("--force");
        }

        startInfo.Environment["PATH"] = isolateFakePath
            ? fakeBin
            : fakeBin + Path.PathSeparator + (Environment.GetEnvironmentVariable("PATH") ?? string.Empty);
        startInfo.Environment["FAKE_DOCKER_LOG"] = dockerLog;
        startInfo.Environment["FAKE_CURL_LOG"] = curlLog;
        startInfo.Environment["FAKE_BLOCKED_LOG"] = Path.Combine(
            packetRoot, "smokes", "smoke-linux-amd64.log");
        startInfo.Environment["FAKE_BLOCKED_SUMMARY"] = Path.Combine(
            packetRoot, "smokes", "smoke-results.json");

        using Process process = Process.Start(startInfo).ShouldNotBeNull();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromSeconds(15)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Smoke capture fake did not exit inside the 15-second test budget.");
        }

        return new ProcessResult(
            process.ExitCode,
            output.GetAwaiter().GetResult(),
            error.GetAwaiter().GetResult());
    }

    private static string DockerFake(string mode) =>
        $$"""
        #!/bin/sh
        printf '%s\n' "$*" >> "$FAKE_DOCKER_LOG"
        if [ "{{mode}}" = "pull-failure" ] && [ "$1" = "pull" ]; then exit 42; fi
        if [ "{{mode}}" = "run-failure" ] && [ "$1" = "run" ]; then exit 44; fi
        if [ "$1" = "port" ]; then printf '%s\n' '127.0.0.1:45678'; exit 0; fi
        if [ "$1" = "inspect" ]; then
          case "$*" in *amd64*) printf '%s\n' 'sha256:amd64' ;; *) printf '%s\n' 'sha256:arm64' ;; esac
          exit 0
        fi
        if [ "$1" = "image" ]; then
          case "$*" in *amd64*) printf '%s\n' 'linux/amd64' ;; *) printf '%s\n' 'linux/arm64' ;; esac
          if [ "{{mode}}" = "log-write-failure" ]; then mkdir -p "$FAKE_BLOCKED_LOG"; fi
          if [ "{{mode}}" = "summary-write-failure" ]; then
            case "$*" in *arm64*) mkdir -p "$FAKE_BLOCKED_SUMMARY" ;; esac
          fi
          if [ "{{mode}}" = "cleanup-oserror" ]; then /bin/rm "$0"; fi
          exit 0
        fi
        if [ "$1" = "rm" ]; then
          if [ "{{mode}}" = "cleanup-command-failure" ]; then exit 43; fi
          if [ "{{mode}}" = "cleanup-timeout" ]; then exec /bin/sleep 5; fi
        fi
        exit 0
        """;

    /// <summary>
    /// A curl fake that answers connection-refused, then a non-200, then 200, so the readiness loop
    /// must actually poll. The first shape reproduces the real curl behaviour on a closed port:
    /// exit 7 with a "000 0" write-out.
    /// </summary>
    /// <param name="counterPath">File used to count invocations per platform.</param>
    /// <returns>The fake script.</returns>
    private static string RetryingCurlFake(string counterPath) =>
        $$"""
        #!/bin/sh
        printf '%s\n' "$*" >> "$FAKE_CURL_LOG"
        counter='{{counterPath}}'
        attempts=0
        if [ -f "$counter" ]; then attempts=$(cat "$counter"); fi
        attempts=$((attempts + 1))
        printf '%s' "$attempts" > "$counter"
        case "$attempts" in
          1) printf '%s\n' '000 0'; exit 7 ;;
          2) printf '%s\n' '503 0'; exit 0 ;;
          3) printf '%s\n' '200 0'; printf '%s' 0 > "$counter"; exit 0 ;;
        esac
        printf '%s\n' '200 0'
        exit 0
        """;

    private static string CurlFake(string output) =>
        $$"""
        #!/bin/sh
        printf '%s\n' "$*" >> "$FAKE_CURL_LOG"
        printf '%s\n' '{{output}}'
        exit 0
        """;

    private static void WriteExecutable(string path, string content)
    {
        File.WriteAllText(path, content.Replace("\r\n", "\n", StringComparison.Ordinal), new UTF8Encoding(false));
        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static JsonObject LoadSummary(string packetRoot) =>
        JsonNode.Parse(File.ReadAllBytes(Path.Combine(packetRoot, "smokes", "smoke-results.json")))!
            .AsObject();

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
