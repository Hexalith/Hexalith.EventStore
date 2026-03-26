using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Tenant;

/// <summary>
/// The <c>eventstore-admin tenant compare</c> subcommand — compares usage across tenants.
/// </summary>
public static class TenantCompareCommand
{
    /// <summary>
    /// Creates the tenant compare subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding)
    {
        Argument<string[]> tenantIdsArg = new("tenantIds")
        {
            Description = "Tenant IDs to compare",
            Arity = new ArgumentArity(2, 100),
        };

        Command command = new("compare", "Compare usage across tenants");
        command.Arguments.Add(tenantIdsArg);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            GlobalOptions options = binding.Resolve(parseResult);
            string[] tenantIds = parseResult.GetValue(tenantIdsArg) ?? [];
            return await ExecuteAsync(options, tenantIds, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    internal static async Task<int> ExecuteAsync(
        GlobalOptions options,
        string[] tenantIds,
        CancellationToken cancellationToken)
    {
        using AdminApiClient client = new(options);
        return await ExecuteAsync(client, options, tenantIds, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string[] tenantIds,
        CancellationToken cancellationToken)
    {
        IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
        OutputWriter writer = new(options.OutputFile);
        try
        {
            object body = new { TenantIds = tenantIds };
            TenantComparison comparison = await client
                .PostAsync<TenantComparison>("api/v1/admin/tenants/compare", body, cancellationToken)
                .ConfigureAwait(false);

            string output;
            if (string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase))
            {
                output = formatter.Format(comparison);
            }
            else
            {
                output = formatter.FormatCollection(comparison.Tenants.ToList(), TenantListCommand.Columns);
            }

            int writeResult = writer.Write(output);
            if (writeResult != ExitCodes.Success)
            {
                return writeResult;
            }

            if (!string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"Compared at: {comparison.ComparedAtUtc}");
            }

            return ExitCodes.Success;
        }
        catch (AdminApiException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.Error;
        }
    }
}
