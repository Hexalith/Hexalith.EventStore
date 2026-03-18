// <copyright file="PrerequisiteValidator.cs" company="ITANEO">
// Copyright (c) ITANEO (https://www.itaneo.com). All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Hexalith.EventStore.AppHost;

/// <summary>
/// Validates that required development prerequisites (Docker, DAPR) are available
/// before Aspire builder initialization. Produces consolidated, actionable error
/// messages with installation links when prerequisites are missing.
/// </summary>
internal static class PrerequisiteValidator
{
    /// <summary>
    /// Checks for required prerequisites and exits with a consolidated error message
    /// if any are missing. Skipped in CI environments and publish scenarios.
    /// </summary>
    public static void Validate()
    {
        // Skip in CI environments and publish scenarios — CI manages its own prerequisites,
        // and `aspire publish` generates manifests without needing Docker/DAPR locally.
        // GitHub Actions sets CI=true; other CI systems can set SKIP_PREREQUISITE_CHECK=true.
        if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Environment.GetEnvironmentVariable("SKIP_PREREQUISITE_CHECK"), "true", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PUBLISH_TARGET")))
        {
            return;
        }

        List<string> errors = [];

        // Check Docker availability (docker info can be slow ~2-3s when Docker Desktop is starting)
        if (!IsCommandAvailable("docker", "info"))
        {
            errors.Add(
                "Docker is not running or not installed.\n"
                + "  Install Docker Desktop: https://docs.docker.com/get-docker/\n"
                + "  Then start Docker Desktop and retry.");
        }

        // Check DAPR CLI + runtime initialization
        (bool cliAvailable, string? daprOutput) = RunCommand("dapr", "--version");
        if (!cliAvailable)
        {
            errors.Add(
                "DAPR CLI is not installed.\n"
                + "  Install DAPR: https://docs.dapr.io/getting-started/install-dapr-cli/\n"
                + "  Then run 'dapr init' and retry.");
        }
        else if (daprOutput is null
            || daprOutput.Contains("Runtime version: n/a", StringComparison.OrdinalIgnoreCase)
            || !daprOutput.Contains("Runtime version:", StringComparison.OrdinalIgnoreCase))
        {
            // CLI is installed but runtime is not initialized (n/a or line absent entirely)
            errors.Add(
                "DAPR runtime is not initialized.\n"
                + "  Run 'dapr init' (recommended for full local development)\n"
                + "  or 'dapr init --slim' (minimal, no Docker components) and retry.");
        }

        if (errors.Count > 0)
        {
            Console.Error.WriteLine("Prerequisites missing:");
            Console.Error.WriteLine();
            foreach (string error in errors)
            {
                Console.Error.WriteLine($"  - {error}");
                Console.Error.WriteLine();
            }

            Environment.Exit(1);
        }
    }

    private static bool IsCommandAvailable(string command, string args)
        => RunCommand(command, args).Success;

    private static (bool Success, string? Output) RunCommand(string command, string args)
    {
        try
        {
            using System.Diagnostics.Process? process = System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                {
                    FileName = command,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });

            // WaitForExit returns false on timeout — must check before accessing ExitCode.
            bool exited = process?.WaitForExit(TimeSpan.FromSeconds(5)) ?? false;
            if (!exited || process is null)
            {
                return (false, null);
            }

            string output = process.StandardOutput.ReadToEnd();
            return (process.ExitCode == 0, output);
        }
        catch (Exception ex)
        {
            return (false, $"Error executing '{command}': {ex.Message}");
        }
    }
}
