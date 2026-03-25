using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Tenant;

/// <summary>
/// The <c>eventstore-admin tenant quotas</c> subcommand — displays tenant quota information.
/// </summary>
public static class TenantQuotasCommand
{
    internal static readonly List<ColumnDefinition> Columns =
    [
        new("Tenant ID", "TenantId"),
        new("Max Events/Day", "MaxEventsPerDay"),
        new("Max Storage", "MaxStorageBytes"),
        new("Current Usage", "CurrentUsage"),
    ];

    /// <summary>
    /// Creates the tenant quotas subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding)
    {
        Argument<string> tenantIdArg = TenantArguments.TenantId();

        Command command = new("quotas", "Show tenant quota usage");
        command.Arguments.Add(tenantIdArg);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            GlobalOptions options = binding.Resolve(parseResult);
            string tenantId = parseResult.GetValue(tenantIdArg)!;
            return await ExecuteAsync(options, tenantId, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    internal static async Task<int> ExecuteAsync(
        GlobalOptions options,
        string tenantId,
        CancellationToken cancellationToken)
    {
        using AdminApiClient client = new(options);
        return await ExecuteAsync(client, options, tenantId, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string tenantId,
        CancellationToken cancellationToken)
    {
        IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
        OutputWriter writer = new(options.OutputFile);
        try
        {
            TenantQuotas? quotas = await client
                .TryGetAsync<TenantQuotas>($"api/v1/admin/tenants/{Uri.EscapeDataString(tenantId)}/quotas", cancellationToken)
                .ConfigureAwait(false);

            if (quotas is null)
            {
                Console.Error.WriteLine($"Tenant '{tenantId}' not found.");
                return ExitCodes.Error;
            }

            int usagePct = (int)(quotas.CurrentUsage * 100 / Math.Max(quotas.MaxStorageBytes, 1));

            string output;
            if (string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase))
            {
                output = formatter.Format(quotas);
            }
            else
            {
                output = formatter.Format(quotas, Columns);
            }

            int writeResult = writer.Write(output);
            if (writeResult != ExitCodes.Success)
            {
                return writeResult;
            }

            Console.Error.WriteLine($"Storage usage: {usagePct}%");
            return MapExitCode(usagePct);
        }
        catch (AdminApiException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.Error;
        }
    }

    internal static int MapExitCode(int usagePercentage) => usagePercentage switch
    {
        >= 90 => ExitCodes.Degraded,
        _ => ExitCodes.Success,
    };
}
