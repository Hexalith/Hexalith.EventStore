using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Projections;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Projection;

/// <summary>
/// The <c>eventstore-admin projection status</c> subcommand — displays detailed projection status.
/// </summary>
public static class ProjectionStatusCommand {
    internal static readonly List<ColumnDefinition> OverviewColumns =
    [
        new("Name", "Name"),
        new("Tenant", "Tenant"),
        new("Status", "Status"),
        new("Lag", "Lag", Align: Alignment.Right),
        new("Throughput", "Throughput", Align: Alignment.Right),
        new("Error Count", "ErrorCount", Align: Alignment.Right),
        new("Last Position", "LastPosition", Align: Alignment.Right),
        new("Last Processed", "LastProcessed"),
        new("Subscribed Events", "SubscribedEvents"),
        new("Configuration", "Configuration", MaxWidth: 80),
    ];

    internal static readonly List<ColumnDefinition> ErrorColumns =
    [
        new("Position", "Position"),
        new("Timestamp", "Timestamp"),
        new("Event Type", "EventTypeName"),
        new("Message", "Message", MaxWidth: 60),
    ];

    /// <summary>
    /// Creates the projection status subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding) {
        Argument<string> tenantArg = ProjectionArguments.Tenant();
        Argument<string> nameArg = ProjectionArguments.Name();

        Command command = new("status", "Show detailed projection status with error information");
        command.Arguments.Add(tenantArg);
        command.Arguments.Add(nameArg);

        command.SetAction(async (parseResult, cancellationToken) => {
            GlobalOptions options = binding.Resolve(parseResult);
            string tenant = parseResult.GetValue(tenantArg)!;
            string name = parseResult.GetValue(nameArg)!;
            return await ExecuteAsync(options, tenant, name, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    internal static async Task<int> ExecuteAsync(
        GlobalOptions options,
        string tenant,
        string name,
        CancellationToken cancellationToken) {
        using AdminApiClient client = new(options);
        return await ExecuteAsync(client, options, tenant, name, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string tenant,
        string name,
        CancellationToken cancellationToken) {
        IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
        OutputWriter writer = new(options.OutputFile);
        try {
            string path = $"api/v1/admin/projections/{Uri.EscapeDataString(tenant)}/{Uri.EscapeDataString(name)}";
            ProjectionDetail? detail = await client
                .TryGetAsync<ProjectionDetail>(path, cancellationToken)
                .ConfigureAwait(false);

            if (detail is null) {
                Console.Error.WriteLine($"Projection '{name}' not found in tenant '{tenant}'.");
                return ExitCodes.Error;
            }

            string output;
            if (string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase)) {
                output = formatter.Format(detail);
            }
            else if (string.Equals(options.Format, "csv", StringComparison.OrdinalIgnoreCase)) {
                if (detail.Errors.Count > 0) {
                    output = formatter.FormatCollection(detail.Errors.ToList(), ErrorColumns);
                }
                else {
                    // AC2: when no errors, render overview as a single CSV row
                    var overview = new {
                        detail.Name,
                        Tenant = detail.TenantId,
                        detail.Status,
                        detail.Lag,
                        detail.Throughput,
                        detail.ErrorCount,
                        LastPosition = detail.LastProcessedPosition,
                        LastProcessed = detail.LastProcessedUtc,
                        SubscribedEvents = string.Join(", ", detail.SubscribedEventTypes),
                        detail.Configuration,
                    };
                    output = formatter.FormatCollection(new[] { overview }, OverviewColumns);
                }
            }
            else {
                // Table format — dual-section (overview + errors)
                var overview = new {
                    detail.Name,
                    Tenant = detail.TenantId,
                    detail.Status,
                    detail.Lag,
                    detail.Throughput,
                    detail.ErrorCount,
                    LastPosition = detail.LastProcessedPosition,
                    LastProcessed = detail.LastProcessedUtc,
                    SubscribedEvents = string.Join(", ", detail.SubscribedEventTypes),
                    detail.Configuration,
                };

                output = formatter.Format(overview, OverviewColumns);

                if (detail.Errors.Count > 0) {
                    output += Environment.NewLine + Environment.NewLine
                        + formatter.FormatCollection(detail.Errors.ToList(), ErrorColumns);
                }
            }

            int writeResult = writer.Write(output);
            return writeResult != ExitCodes.Success ? writeResult : ExitCodes.Success;
        }
        catch (AdminApiException ex) {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.Error;
        }
    }
}
