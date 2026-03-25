using System.CommandLine;
using System.Diagnostics;

using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands;

/// <summary>
/// The <c>eventstore-admin health</c> command — calls GET /api/v1/admin/health and displays the system health report.
/// Supports CI/CD options: --strict, --wait, --timeout, --quiet.
/// Also serves as parent command for the <c>health dapr</c> sub-subcommand.
/// </summary>
public static class HealthCommand
{
    /// <summary>
    /// Creates the health command wired to the shared global options.
    /// Includes CI/CD options and the <c>dapr</c> sub-subcommand.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding)
    {
        Option<bool> strictOption = new("--strict") { Description = "Treat degraded status as error (exit code 2)" };
        strictOption.DefaultValueFactory = _ => false;

        Option<bool> waitOption = new("--wait") { Description = "Poll until healthy or timeout" };
        waitOption.DefaultValueFactory = _ => false;

        Option<int> timeoutOption = new("--timeout") { Description = "Maximum wait time in seconds (requires --wait)" };
        timeoutOption.DefaultValueFactory = _ => 30;
        timeoutOption.Validators.Add(result =>
        {
            int value = result.GetValue(timeoutOption);
            if (value < 1)
            {
                result.AddError("Timeout must be at least 1 second.");
            }
        });

        Option<bool> quietOption = new("--quiet", "-q") { Description = "Suppress stdout output, only return exit code" };
        quietOption.DefaultValueFactory = _ => false;

        Command command = new("health", "Show system health status");
        command.Options.Add(strictOption);
        command.Options.Add(waitOption);
        command.Options.Add(timeoutOption);
        command.Options.Add(quietOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            GlobalOptions options = binding.Resolve(parseResult);
            bool strict = parseResult.GetValue(strictOption);
            bool wait = parseResult.GetValue(waitOption);
            int timeout = parseResult.GetValue(timeoutOption);
            bool quiet = parseResult.GetValue(quietOption);
            return await ExecuteAsync(options, strict, wait, timeout, quiet, cancellationToken)
                .ConfigureAwait(false);
        });

        command.Subcommands.Add(HealthDaprCommand.Create(binding));

        return command;
    }

    internal static async Task<int> ExecuteAsync(
        GlobalOptions options,
        bool strict,
        bool wait,
        int timeout,
        bool quiet,
        CancellationToken cancellationToken,
        int pollIntervalMs = 1000)
    {
        using AdminApiClient client = new(options);
        return await ExecuteAsync(client, options, strict, wait, timeout, quiet, cancellationToken, pollIntervalMs)
            .ConfigureAwait(false);
    }

    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        bool strict,
        bool wait,
        int timeout,
        bool quiet,
        CancellationToken cancellationToken,
        int pollIntervalMs = 1000)
    {
        IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
        OutputWriter writer = new(options.OutputFile);

        if (wait)
        {
            return await ExecuteWaitAsync(client, options, strict, timeout, quiet, formatter, writer, cancellationToken, pollIntervalMs)
                .ConfigureAwait(false);
        }

        try
        {
            SystemHealthReport report = await client
                .GetAsync<SystemHealthReport>("/api/v1/admin/health", cancellationToken)
                .ConfigureAwait(false);

            if (!quiet || options.OutputFile is not null)
            {
                string output = FormatHealthReport(report, options.Format, formatter);
                int writeResult = writer.Write(output);
                if (writeResult != ExitCodes.Success)
                {
                    return writeResult;
                }
            }

            return MapExitCode(report.OverallStatus, strict);
        }
        catch (AdminApiException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.Error;
        }
    }

    internal static int MapExitCode(HealthStatus status, bool strict) => status switch
    {
        HealthStatus.Healthy => ExitCodes.Success,
        HealthStatus.Degraded => strict ? ExitCodes.Error : ExitCodes.Degraded,
        _ => ExitCodes.Error,
    };

    internal static bool IsAcceptable(HealthStatus status, bool strict)
        => strict ? status == HealthStatus.Healthy : status != HealthStatus.Unhealthy;

    private static async Task<int> ExecuteWaitAsync(
        AdminApiClient client,
        GlobalOptions options,
        bool strict,
        int timeout,
        bool quiet,
        IOutputFormatter formatter,
        OutputWriter writer,
        CancellationToken cancellationToken,
        int pollIntervalMs)
    {
        Stopwatch sw = Stopwatch.StartNew();
        int attempt = 0;
        while (sw.Elapsed.TotalSeconds < timeout)
        {
            attempt++;
            try
            {
                SystemHealthReport report = await client
                    .GetAsync<SystemHealthReport>("/api/v1/admin/health", cancellationToken)
                    .ConfigureAwait(false);

                if (IsAcceptable(report.OverallStatus, strict))
                {
                    Console.Error.WriteLine("Service is healthy.");
                    if (!quiet || options.OutputFile is not null)
                    {
                        string output = FormatHealthReport(report, options.Format, formatter);
                        int writeResult = writer.Write(output);
                        if (writeResult != ExitCodes.Success)
                        {
                            return writeResult;
                        }
                    }

                    return MapExitCode(report.OverallStatus, strict);
                }
            }
            catch (AdminApiException) when (!cancellationToken.IsCancellationRequested)
            {
                // Connection errors during polling are not fatal — retry
            }

            Console.Error.WriteLine($"Waiting for healthy status... (attempt {attempt})");
            await Task.Delay(pollIntervalMs, cancellationToken).ConfigureAwait(false);
        }

        Console.Error.WriteLine($"Timed out waiting for healthy status after {timeout} seconds.");
        return ExitCodes.Error;
    }

    private static string FormatHealthReport(SystemHealthReport report, string format, IOutputFormatter formatter)
    {
        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            return formatter.Format(report);
        }
        else if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            List<ColumnDefinition> columns =
            [
                new("ComponentName", "ComponentName"),
                new("ComponentType", "ComponentType"),
                new("Status", "Status"),
                new("LastCheckUtc", "LastCheckUtc"),
            ];
            return formatter.FormatCollection(report.DaprComponents.ToList(), columns);
        }
        else
        {
            // Table format — two sections
            int healthyCount = report.DaprComponents.Count(c => c.Status == HealthStatus.Healthy);
            int degradedCount = report.DaprComponents.Count(c => c.Status == HealthStatus.Degraded);
            int unhealthyCount = report.DaprComponents.Count(c => c.Status == HealthStatus.Unhealthy);

            var overview = new
            {
                OverallStatus = report.OverallStatus,
                TotalEvents = report.TotalEventCount,
                EventsPerSec = report.EventsPerSecond.ToString("F1"),
                ErrorPercent = report.ErrorPercentage.ToString("F2"),
                DaprComponents = report.DaprComponents.Count,
                Healthy = healthyCount,
                Degraded = degradedCount,
                Unhealthy = unhealthyCount,
            };
            List<ColumnDefinition> columns =
            [
                new("Component Name", "ComponentName"),
                new("Type", "ComponentType"),
                new("Status", "Status"),
                new("Last Check", "LastCheckUtc"),
            ];

            return string.Concat(
                formatter.Format(overview),
                Environment.NewLine,
                Environment.NewLine,
                formatter.FormatCollection(report.DaprComponents.ToList(), columns));
        }
    }
}
