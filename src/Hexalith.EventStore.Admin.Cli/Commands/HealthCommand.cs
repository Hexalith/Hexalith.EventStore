using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands;

/// <summary>
/// The <c>eventstore-admin health</c> subcommand — calls GET /api/v1/admin/health and displays the system health report.
/// </summary>
public static class HealthCommand
{
    /// <summary>
    /// Creates the health subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding)
    {
        Command command = new("health", "Show system health status");
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            GlobalOptions options = binding.Resolve(parseResult);
            return await ExecuteAsync(options, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    internal static async Task<int> ExecuteAsync(GlobalOptions options, CancellationToken cancellationToken)
    {
        using AdminApiClient client = new(options);
        IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
        OutputWriter writer = new(options.OutputFile);
        try
        {
            SystemHealthReport report = await client
                .GetAsync<SystemHealthReport>("/api/v1/admin/health", cancellationToken)
                .ConfigureAwait(false);

            string output;
            if (string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase))
            {
                output = formatter.Format(report);
            }
            else if (string.Equals(options.Format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                List<ColumnDefinition> columns =
                [
                    new("ComponentName", "ComponentName"),
                    new("ComponentType", "ComponentType"),
                    new("Status", "Status"),
                    new("LastCheckUtc", "LastCheckUtc"),
                ];
                output = formatter.FormatCollection(report.DaprComponents.ToList(), columns);
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

                output = string.Concat(
                    formatter.Format(overview),
                    Environment.NewLine,
                    Environment.NewLine,
                    formatter.FormatCollection(report.DaprComponents.ToList(), columns));
            }

            int writeResult = writer.Write(output);
            if (writeResult != ExitCodes.Success)
            {
                return writeResult;
            }

            return report.OverallStatus switch
            {
                HealthStatus.Healthy => ExitCodes.Success,
                HealthStatus.Degraded => ExitCodes.Degraded,
                _ => ExitCodes.Error,
            };
        }
        catch (AdminApiException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.Error;
        }
    }
}
