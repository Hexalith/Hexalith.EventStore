using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Projections;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Projection;

/// <summary>
/// The <c>eventstore-admin projection list</c> subcommand — lists projection statuses.
/// </summary>
public static class ProjectionListCommand {
    internal static readonly List<ColumnDefinition> Columns =
    [
        new("Name", "Name"),
        new("Tenant", "TenantId"),
        new("Status", "Status"),
        new("Lag", "Lag", Align: Alignment.Right),
        new("Throughput", "Throughput", Align: Alignment.Right),
        new("Errors", "ErrorCount", Align: Alignment.Right),
        new("Last Position", "LastProcessedPosition", Align: Alignment.Right),
        new("Last Processed", "LastProcessedUtc"),
    ];

    /// <summary>
    /// Creates the projection list subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding) {
        Option<string?> tenantOption = new("--tenant", "-T") { Description = "Filter by tenant identifier" };

        Command command = new("list", "List projections and their statuses");
        command.Options.Add(tenantOption);

        command.SetAction(async (parseResult, cancellationToken) => {
            GlobalOptions options = binding.Resolve(parseResult);
            string? tenant = parseResult.GetValue(tenantOption);
            return await ExecuteAsync(options, tenant, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    internal static async Task<int> ExecuteAsync(
        GlobalOptions options,
        string? tenant,
        CancellationToken cancellationToken) {
        using AdminApiClient client = new(options);
        return await ExecuteAsync(client, options, tenant, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string? tenant,
        CancellationToken cancellationToken) {
        IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
        OutputWriter writer = new(options.OutputFile);
        try {
            string path = "api/v1/admin/projections";
            if (tenant is not null) {
                path += $"?tenantId={Uri.EscapeDataString(tenant)}";
            }

            List<ProjectionStatus> result = await client
                .GetAsync<List<ProjectionStatus>>(path, cancellationToken)
                .ConfigureAwait(false);

            if (result.Count == 0) {
                Console.Error.WriteLine("No projections found.");
                return ExitCodes.Success;
            }

            string output;
            if (string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase)) {
                output = formatter.Format(result);
            }
            else {
                output = formatter.FormatCollection(result, Columns);
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
