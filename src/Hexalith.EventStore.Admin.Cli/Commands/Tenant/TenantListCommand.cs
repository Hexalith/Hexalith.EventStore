using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Tenant;

/// <summary>
/// The <c>eventstore-admin tenant list</c> subcommand — lists all tenants.
/// </summary>
public static class TenantListCommand {
    internal static readonly List<ColumnDefinition> Columns =
    [
        new("Tenant ID", "TenantId"),
        new("Name", "Name"),
        new("Status", "Status"),
    ];

    /// <summary>
    /// Creates the tenant list subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding) {
        Command command = new("list", "List all tenants");

        command.SetAction(async (parseResult, cancellationToken) => {
            GlobalOptions options = binding.Resolve(parseResult);
            return await ExecuteAsync(options, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    internal static async Task<int> ExecuteAsync(
        GlobalOptions options,
        CancellationToken cancellationToken) {
        using AdminApiClient client = new(options);
        return await ExecuteAsync(client, options, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        CancellationToken cancellationToken) {
        IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
        OutputWriter writer = new(options.OutputFile);
        try {
            List<TenantSummary> result = await client
                .GetAsync<List<TenantSummary>>("api/v1/admin/tenants", cancellationToken)
                .ConfigureAwait(false);

            if (result.Count == 0) {
                Console.Error.WriteLine("No tenants found.");
                return ExitCodes.Success;
            }

            string output;
            if (string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase)) {
                output = formatter.Format(result);
            }
            else {
                output = formatter.FormatCollection(result, Columns);
            }

            return writer.Write(output);
        }
        catch (AdminApiException ex) {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.Error;
        }
    }
}
