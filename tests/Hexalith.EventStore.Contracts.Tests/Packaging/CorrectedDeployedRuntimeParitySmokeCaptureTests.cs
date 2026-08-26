using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Executes the Story 3.15 smoke-capture utility against recording and failing command fakes.
/// </summary>
public sealed class CorrectedDeployedRuntimeParitySmokeCaptureTests
{
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
        Xunit.Assert.SkipUnless(OperatingSystem.IsLinux(), "Smoke command fakes require Linux shell scripts.");
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
                System.Text.RegularExpressions.Match timeout = System.Text.RegularExpressions.Regex.Match(
                    line,
                    "--max-time ([0-9]+(?:\\.[0-9]+)?)");
                timeout.Success.ShouldBeTrue(line);
                double.Parse(timeout.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)
                    .ShouldBeInRange(0.001, 5.0);
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
        Xunit.Assert.SkipUnless(OperatingSystem.IsLinux(), "Smoke command fakes require Linux shell scripts.");
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
            result.Error.ShouldContain("rerun: ");
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
    /// Verifies cleanup is passed only the remaining platform deadline: a hanging fake is timed
    /// out promptly and the failure record is still written.
    /// </summary>
    [Fact]
    public void CleanupTimeoutIsBoundedAndRetained()
    {
        Xunit.Assert.SkipUnless(OperatingSystem.IsLinux(), "Smoke command fakes require Linux shell scripts.");
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
                timeoutOverride: 0.2);
            elapsed.Stop();

            result.ExitCode.ShouldBe(1, result.Error);
            elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
            JsonObject summary = LoadSummary(temporary);
            summary["result"]!.GetValue<string>().ShouldBe("failure");
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
    /// Verifies one transient readiness failure is retried and the second exact HTTP 200 response
    /// succeeds for each platform within the shared deadline.
    /// </summary>
    [Fact]
    public void TransientReadinessFailureRetriesWithinTheBoundedBudget()
    {
        Xunit.Assert.SkipUnless(OperatingSystem.IsLinux(), "Smoke command fakes require Linux shell scripts.");
        string root = FindRepositoryRoot();
        string temporary = Directory.CreateTempSubdirectory("eventstore-story315-smoke-retry-").FullName;
        try
        {
            string fakeBin = Path.Combine(temporary, "bin");
            Directory.CreateDirectory(fakeBin);
            string dockerLog = Path.Combine(temporary, "docker-argv.log");
            string curlLog = Path.Combine(temporary, "curl-argv.log");
            WriteExecutable(Path.Combine(fakeBin, "docker"), DockerFake("success"));
            WriteExecutable(Path.Combine(fakeBin, "curl"), CurlFake("transient"));

            ProcessResult result = RunCapture(root, temporary, fakeBin, dockerLog, curlLog);

            result.ExitCode.ShouldBe(0, result.Error);
            File.ReadAllLines(curlLog).Length.ShouldBe(4);
            LoadSummary(temporary)["platforms"]!.AsArray().ShouldAllBe(item =>
                item!["attempts"]!.GetValue<int>() == 2
                && item["http_status"]!.GetValue<int>() == 200);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies an exhausted capture deadline still invokes forced container cleanup under its own
    /// bounded budget.
    /// </summary>
    [Fact]
    public void ExpiredCaptureDeadlineStillAttemptsCleanup()
    {
        Xunit.Assert.SkipUnless(OperatingSystem.IsLinux(), "Smoke command fakes require Linux shell scripts.");
        string root = FindRepositoryRoot();
        string temporary = Directory.CreateTempSubdirectory("eventstore-story315-smoke-expired-").FullName;
        try
        {
            string fakeBin = Path.Combine(temporary, "bin");
            Directory.CreateDirectory(fakeBin);
            string dockerLog = Path.Combine(temporary, "docker-argv.log");
            string curlLog = Path.Combine(temporary, "curl-argv.log");
            WriteExecutable(Path.Combine(fakeBin, "docker"), DockerFake("success"));
            WriteExecutable(Path.Combine(fakeBin, "curl"), CurlFake("timeout"));

            ProcessResult result = RunCapture(
                root,
                temporary,
                fakeBin,
                dockerLog,
                curlLog,
                timeoutOverride: 0.2);

            result.ExitCode.ShouldBe(1, result.Error);
            File.ReadAllLines(dockerLog).Count(line => line.StartsWith("rm --force ", StringComparison.Ordinal))
                .ShouldBe(2);
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
    /// Verifies capture refuses to overwrite retained smoke bytes unless the operator explicitly
    /// supplies <c>--force</c>.
    /// </summary>
    [Fact]
    public void ExistingSmokeEvidenceIsNotOverwrittenImplicitly()
    {
        Xunit.Assert.SkipUnless(OperatingSystem.IsLinux(), "Smoke command fakes require Linux shell scripts.");
        string root = FindRepositoryRoot();
        string temporary = Directory.CreateTempSubdirectory("eventstore-story315-smoke-existing-").FullName;
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
            File.WriteAllText(retained, "retained-bytes");

            ProcessResult result = RunCapture(root, temporary, fakeBin, dockerLog, curlLog);

            result.ExitCode.ShouldBe(1, result.Error);
            result.Error.ShouldContain("retained smoke evidence already exists");
            result.Error.ShouldContain("rerun: ");
            File.ReadAllText(retained).ShouldBe("retained-bytes");
            File.Exists(dockerLog).ShouldBeFalse();
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
        bool isolateFakePath = false)
    {
        string script = Path.Combine(root, "tools", "capture-corrected-deployed-runtime-parity-smokes.py");
        ProcessStartInfo startInfo = new(ResolveExecutable("python3"))
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        if (timeoutOverride is null)
        {
            startInfo.ArgumentList.Add(script);
        }
        else
        {
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(
                "import importlib.util,sys;" +
                "s=importlib.util.spec_from_file_location('story315_smoke',sys.argv[1]);" +
                "m=importlib.util.module_from_spec(s);s.loader.exec_module(m);" +
                $"m.TIMEOUT_SECONDS={timeoutOverride.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)};" +
                "sys.argv=[sys.argv[1],sys.argv[2]];raise SystemExit(m.main())");
            startInfo.ArgumentList.Add(script);
        }

        startInfo.ArgumentList.Add(packetRoot);
        startInfo.Environment["PATH"] = isolateFakePath
            ? fakeBin
            : fakeBin + Path.PathSeparator + (Environment.GetEnvironmentVariable("PATH") ?? string.Empty);
        startInfo.Environment["FAKE_DOCKER_LOG"] = dockerLog;
        startInfo.Environment["FAKE_CURL_LOG"] = curlLog;

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
        if [ "$1" = "port" ]; then printf '%s\n' '127.0.0.1:45678'; exit 0; fi
        if [ "$1" = "inspect" ]; then
          case "$*" in *amd64*) printf '%s\n' 'sha256:amd64' ;; *) printf '%s\n' 'sha256:arm64' ;; esac
          exit 0
        fi
        if [ "$1" = "image" ]; then
          case "$*" in *amd64*) printf '%s\n' 'linux/amd64' ;; *) printf '%s\n' 'linux/arm64' ;; esac
          if [ "{{mode}}" = "cleanup-oserror" ]; then /bin/rm "$0"; fi
          exit 0
        fi
        if [ "$1" = "rm" ]; then
          if [ "{{mode}}" = "cleanup-command-failure" ]; then exit 43; fi
          if [ "{{mode}}" = "cleanup-timeout" ]; then exec /bin/sleep 5; fi
        fi
        exit 0
        """;

    private static string CurlFake(string output) =>
        $$"""
        #!/bin/sh
        printf '%s\n' "$*" >> "$FAKE_CURL_LOG"
        if [ '{{output}}' = 'timeout' ]; then exec /bin/sleep 5; fi
        if [ '{{output}}' = 'transient' ]; then
          count=$(/usr/bin/wc -l < "$FAKE_CURL_LOG")
          if [ $((count % 2)) -eq 1 ]; then printf '%s\n' '503 0'; else printf '%s\n' '200 0'; fi
          exit 0
        fi
        printf '%s\n' '{{output}}'
        exit 0
        """;

    private static void WriteExecutable(string path, string content)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Smoke command fakes require Linux file modes.");
        }

        File.WriteAllText(path, content.Replace("\r\n", "\n", StringComparison.Ordinal), new UTF8Encoding(false));
        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static JsonObject LoadSummary(string packetRoot) =>
        JsonNode.Parse(File.ReadAllBytes(Path.Combine(packetRoot, "smokes", "smoke-results.json")))!
            .AsObject();

    private static string ResolveExecutable(string name)
    {
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Executable '{name}' was not found on PATH.");
    }

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
