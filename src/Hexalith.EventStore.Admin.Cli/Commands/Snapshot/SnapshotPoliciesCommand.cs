using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Snapshot;

/// <summary>
/// The <c>eventstore-admin snapshot policies</c> subcommand — lists snapshot policies.
/// </summary>
public static class SnapshotPoliciesCommand {
    internal static readonly List<ColumnDefinition> Columns =
    [
        new("Tenant ID", "TenantId"),
        new("Domain", "Domain"),
        new("Aggregate Type", "AggregateType"),
        new("Interval", "IntervalEvents", Align: Alignment.Right),
        new("Created", "CreatedAtUtc"),
    ];

    /// <summary>
    /// Creates the snapshot policies subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding) {
        Option<string?> tenantOption = new("--tenant", "-T") { Description = "Filter by tenant identifier" };

        Command command = new("policies", "List snapshot policies");
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
        string? tenantId,
        CancellationToken cancellationToken) {
        IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
        OutputWriter writer = new(options.OutputFile);
        try {
            string path = "api/v1/admin/storage/snapshot-policies";
            if (tenantId is not null) {
                path += $"?tenantId={Uri.EscapeDataString(tenantId)}";
            }

            List<SnapshotPolicy> result = await client
                .GetAsync<List<SnapshotPolicy>>(path, cancellationToken)
                .ConfigureAwait(false);

            if (result.Count == 0) {
                Console.Error.WriteLine("No snapshot policies found.");
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
