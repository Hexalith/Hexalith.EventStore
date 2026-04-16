
using System.Diagnostics;

namespace Hexalith.EventStore.Admin.Mcp.Tests;

public class ConfigurationValidationTests {
    // Use the MCP project's own output directory (not the test bin) so that
    // the .deps.json resolves transitive dependencies like Microsoft.Extensions.Hosting.
    private static readonly string _mcpDll = ResolveMcpDll();

    private static string ResolveMcpDll() {
        // Navigate from test assembly to repo root, then to the MCP project's output.
        // Test bin: tests/Hexalith.EventStore.Admin.Mcp.Tests/bin/<config>/<tfm>/
        // MCP bin:  src/Hexalith.EventStore.Admin.Mcp/bin/<config>/<tfm>/
        string testDir = Path.GetDirectoryName(typeof(ConfigurationValidationTests).Assembly.Location)!;
        string config = new DirectoryInfo(testDir).Parent!.Name;  // Debug or Release
        string tfm = new DirectoryInfo(testDir).Name;             // net10.0
        string repoRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        string mcpDll = Path.Combine(repoRoot, "src", "Hexalith.EventStore.Admin.Mcp", "bin", config, tfm, "Hexalith.EventStore.Admin.Mcp.dll");
        return File.Exists(mcpDll) ? mcpDll : typeof(AdminApiClient).Assembly.Location;
    }

    [Fact]
    public void BothEnvVarsMissing_ErrorMentionsBothVariableNames() {
        (string stderr, int exitCode) = RunMcpProcess(envVars: []);

        exitCode.ShouldNotBe(0);
        stderr.ShouldContain("EVENTSTORE_ADMIN_URL");
        stderr.ShouldContain("EVENTSTORE_ADMIN_TOKEN");
    }

    [Fact]
    public void OnlyUrlMissing_ErrorMentionsUrl() {
        (string stderr, int exitCode) = RunMcpProcess(envVars: new Dictionary<string, string?> {
            ["EVENTSTORE_ADMIN_TOKEN"] = "test-token",
        });

        exitCode.ShouldNotBe(0);
        stderr.ShouldContain("EVENTSTORE_ADMIN_URL");
    }

    [Fact]
    public void OnlyTokenMissing_ErrorMentionsToken() {
        (string stderr, int exitCode) = RunMcpProcess(envVars: new Dictionary<string, string?> {
            ["EVENTSTORE_ADMIN_URL"] = "https://localhost:5443",
        });

        exitCode.ShouldNotBe(0);
        stderr.ShouldContain("EVENTSTORE_ADMIN_TOKEN");
    }

    [Fact]
    public void InvalidUri_ErrorMentionsInvalidUri() {
        (string stderr, int exitCode) = RunMcpProcess(envVars: new Dictionary<string, string?> {
            ["EVENTSTORE_ADMIN_URL"] = "not-a-url",
            ["EVENTSTORE_ADMIN_TOKEN"] = "test-token",
        });

        exitCode.ShouldNotBe(0);
        stderr.ShouldContain("not-a-url");
        stderr.ShouldContain("not a valid absolute HTTP(S) URI");
    }

    [Fact]
    public void NonHttpScheme_ErrorMentionsInvalidUri() {
        (string stderr, int exitCode) = RunMcpProcess(envVars: new Dictionary<string, string?> {
            ["EVENTSTORE_ADMIN_URL"] = "ftp://example.com",
            ["EVENTSTORE_ADMIN_TOKEN"] = "test-token",
        });

        exitCode.ShouldNotBe(0);
        stderr.ShouldContain("ftp://example.com");
        stderr.ShouldContain("not a valid absolute HTTP(S) URI");
    }

    [Fact]
    public void ValidConfig_DoesNotExitWithValidationError() {
        // When both env vars are valid, the server should NOT exit with an error.
        // It will block on stdin waiting for MCP input — so we assert it hasn't exited
        // with an error within 2 seconds, then kill it.
        using Process process = CreateProcess(new Dictionary<string, string?> {
            ["EVENTSTORE_ADMIN_URL"] = "https://localhost:5443",
            ["EVENTSTORE_ADMIN_TOKEN"] = "test-token",
        });

        _ = process.Start();
        bool exited = process.WaitForExit(2000);

        if (!exited) {
            // Process is still running (blocked on stdin) — this is the expected behavior.
            process.Kill();
            _ = process.WaitForExit(5000);

            // The server did NOT exit with a validation error, which is correct.
            return;
        }

        // If it exited, it should NOT have been a validation error (exit code 1 with env var error).
        string stderr = process.StandardError.ReadToEnd();
        if (process.ExitCode != 0 && stderr.Contains("EVENTSTORE_ADMIN_URL")) {
            Assert.Fail($"Server exited with validation error: {stderr}");
        }
    }

    private static (string Stderr, int ExitCode) RunMcpProcess(Dictionary<string, string?> envVars) {
        using Process process = CreateProcess(envVars);

        _ = process.Start();
        string stderr = process.StandardError.ReadToEnd();
        bool exited = process.WaitForExit(5000);
        if (!exited) {
            process.Kill();
            _ = process.WaitForExit(5000);
        }

        return (stderr, process.ExitCode);
    }

    private static Process CreateProcess(Dictionary<string, string?> envVars) {
        var process = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = "dotnet",
                Arguments = $"\"{_mcpDll}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        // Clear relevant env vars first, then set provided ones
        process.StartInfo.Environment["EVENTSTORE_ADMIN_URL"] = null;
        process.StartInfo.Environment["EVENTSTORE_ADMIN_TOKEN"] = null;

        foreach (KeyValuePair<string, string?> kvp in envVars) {
            process.StartInfo.Environment[kvp.Key] = kvp.Value;
        }

        return process;
    }
}
