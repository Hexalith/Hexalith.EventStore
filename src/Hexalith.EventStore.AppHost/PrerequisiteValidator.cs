// <copyright file="PrerequisiteValidator.cs" company="ITANEO">
// Copyright (c) ITANEO (https://www.itaneo.com). All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Hexalith.EventStore.AppHost;

internal sealed record PrerequisiteCommandResult(bool Success, string? Output);

internal interface IPrerequisiteCommandRunner
{
    PrerequisiteCommandResult Run(string command, string args, TimeSpan timeout);
}

/// <summary>
/// Validates that required development prerequisites (Docker, DAPR) are available
/// before Aspire builder initialization. Produces consolidated, actionable error
/// messages with installation links when prerequisites are missing.
/// </summary>
internal static class PrerequisiteValidator {
    internal static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(120);
    private const string DockerProbeArguments = "version --format \"{{.Server.Version}}\"";

    /// <summary>
    /// Checks for required prerequisites and exits with a consolidated error message
    /// if any are missing. Skipped in CI environments and publish scenarios.
    /// </summary>
    public static void Validate() {
        // Skip in CI environments and publish scenarios — CI manages its own prerequisites,
        // and `aspire publish` generates manifests without needing Docker/DAPR locally.
        // GitHub Actions sets CI=true; other CI systems can set SKIP_PREREQUISITE_CHECK=true.
        if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Environment.GetEnvironmentVariable("SKIP_PREREQUISITE_CHECK"), "true", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PUBLISH_TARGET"))) {
            return;
        }

        IReadOnlyList<string> errors = GetMissingPrerequisites(new ProcessPrerequisiteCommandRunner(), CommandTimeout);

        if (errors.Count > 0) {
            Console.Error.WriteLine("Prerequisites missing:");
            Console.Error.WriteLine();
            foreach (string error in errors) {
                Console.Error.WriteLine($"  - {error}");
                Console.Error.WriteLine();
            }

            Environment.Exit(1);
        }
    }

    internal static IReadOnlyList<string> GetMissingPrerequisites(
        IPrerequisiteCommandRunner commandRunner,
        TimeSpan commandTimeout) {
        ArgumentNullException.ThrowIfNull(commandRunner);

        List<string> errors = [];

        // Docker Desktop can be slow immediately after startup. Query the server version instead of
        // the heavier `docker info` payload to prove the engine is reachable.
        if (!commandRunner.Run("docker", DockerProbeArguments, commandTimeout).Success) {
            errors.Add(
                "Docker is not running or not installed.\n"
                + "  Install Docker Desktop: https://docs.docker.com/get-docker/\n"
                + "  Then start Docker Desktop and retry.");
        }

        // Check DAPR CLI + runtime initialization
        PrerequisiteCommandResult daprResult = commandRunner.Run("dapr", "--version", commandTimeout);
        if (!daprResult.Success) {
            errors.Add(
                "DAPR CLI is not installed.\n"
                + "  Install DAPR: https://docs.dapr.io/getting-started/install-dapr-cli/\n"
                + "  Then run 'dapr init' and retry.");
        }
        else if (daprResult.Output is null
            || daprResult.Output.Contains("Runtime version: n/a", StringComparison.OrdinalIgnoreCase)
            || !daprResult.Output.Contains("Runtime version:", StringComparison.OrdinalIgnoreCase)) {
            // CLI is installed but runtime is not initialized (n/a or line absent entirely)
            errors.Add(
                "DAPR runtime is not initialized.\n"
                + "  Run 'dapr init' (recommended for full local development)\n"
                + "  or 'dapr init --slim' (minimal, no Docker components) and retry.");
        }

        return errors;
    }

    private sealed class ProcessPrerequisiteCommandRunner : IPrerequisiteCommandRunner {
        public PrerequisiteCommandResult Run(string command, string args, TimeSpan timeout) {
            try {
                using var process = System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo {
                        FileName = command,
                        Arguments = args,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    });

                if (process is null) {
                    return new PrerequisiteCommandResult(false, null);
                }

                // Drain both pipes concurrently before WaitForExit to prevent deadlock:
                // if the child fills either kernel buffer, it blocks on write while we
                // block on WaitForExit — neither side makes progress.
                Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
                Task<string> stderrTask = process.StandardError.ReadToEndAsync();

                // WaitForExit returns false on timeout; check before accessing ExitCode.
                bool exited = process.WaitForExit(timeout);
                if (!exited) {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit();
                    // Drain both tasks to prevent unobserved-exception faults after dispose.
                    try { Task.WhenAll(stdoutTask, stderrTask).GetAwaiter().GetResult(); } catch { }
                    return new PrerequisiteCommandResult(false, null);
                }

                string stdout = stdoutTask.GetAwaiter().GetResult();
                // Some CLI tools (including dapr) write version info to stderr; fall back if stdout is empty.
                string stderr = stderrTask.GetAwaiter().GetResult();
                string output = string.IsNullOrEmpty(stdout) ? stderr : stdout;
                return new PrerequisiteCommandResult(process.ExitCode == 0, output);
            }
            catch (Exception ex) {
                return new PrerequisiteCommandResult(false, $"Error executing '{command}': {ex.Message}");
            }
        }
    }
}
