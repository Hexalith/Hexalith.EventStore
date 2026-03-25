using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands;

/// <summary>
/// The <c>eventstore-admin health dapr</c> sub-subcommand — calls GET /api/v1/admin/health/dapr
/// and displays DAPR component health statuses.
/// </summary>
public static class HealthDaprCommand
{
    internal static readonly IReadOnlyList<ColumnDefinition> Columns =
    [
        new("Component Name", "ComponentName"),
        new("Type", "ComponentType"),
        new("Status", "Status"),
        new("Last Check", "LastCheckUtc"),
    ];

    /// <summary>
    /// Creates the health dapr sub-subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding)
    {
        Option<string?> componentOption = new("--component", "-c") { Description = "Filter by component name" };

        Command command = new("dapr", "Show DAPR component health status");
        command.Options.Add(componentOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            GlobalOptions options = binding.Resolve(parseResult);
            string? component = parseResult.GetValue(componentOption);
            return await ExecuteAsync(options, component, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    internal static async Task<int> ExecuteAsync(
        GlobalOptions options,
        string? component,
        CancellationToken cancellationToken)
    {
        using AdminApiClient client = new(options);
        return await ExecuteAsync(client, options, component, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string? component,
        CancellationToken cancellationToken)
    {
        IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
        OutputWriter writer = new(options.OutputFile);
        try
        {
            List<DaprComponentHealth> components = await client
                .GetAsync<List<DaprComponentHealth>>("/api/v1/admin/health/dapr", cancellationToken)
                .ConfigureAwait(false);

            if (component is not null)
            {
                DaprComponentHealth? match = components.FirstOrDefault(
                    c => string.Equals(c.ComponentName, component, StringComparison.OrdinalIgnoreCase));

                if (match is null)
                {
                    Console.Error.WriteLine($"DAPR component '{component}' not found.");
                    return ExitCodes.Error;
                }

                string singleOutput;
                if (string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase))
                {
                    singleOutput = formatter.Format(match);
                }
                else
                {
                    singleOutput = formatter.FormatCollection([match], Columns);
                }

                int singleWriteResult = writer.Write(singleOutput);
                if (singleWriteResult != ExitCodes.Success)
                {
                    return singleWriteResult;
                }

                return match.Status switch
                {
                    HealthStatus.Healthy => ExitCodes.Success,
                    HealthStatus.Degraded => ExitCodes.Degraded,
                    _ => ExitCodes.Error,
                };
            }

            if (components.Count == 0)
            {
                Console.Error.WriteLine("No DAPR components found.");
                return ExitCodes.Error;
            }

            string output;
            if (string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase))
            {
                output = formatter.Format(components);
            }
            else
            {
                output = formatter.FormatCollection(components, Columns);
            }

            int writeResult = writer.Write(output);
            if (writeResult != ExitCodes.Success)
            {
                return writeResult;
            }

            return DeriveExitCode(components);
        }
        catch (AdminApiException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.Error;
        }
    }

    internal static int DeriveExitCode(IReadOnlyList<DaprComponentHealth> components)
    {
        if (components.Count == 0)
        {
            return ExitCodes.Error;
        }

        if (components.Any(c => c.Status == HealthStatus.Unhealthy))
        {
            return ExitCodes.Error;
        }

        if (components.Any(c => c.Status == HealthStatus.Degraded))
        {
            return ExitCodes.Degraded;
        }

        return ExitCodes.Success;
    }
}
