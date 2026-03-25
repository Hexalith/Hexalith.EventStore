using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Tenant;

/// <summary>
/// The <c>eventstore-admin tenant detail</c> subcommand — displays detailed tenant information.
/// </summary>
public static class TenantDetailCommand
{
    internal static readonly List<ColumnDefinition> OverviewColumns =
    [
        new("Tenant ID", "TenantId"),
        new("Display Name", "DisplayName"),
        new("Status", "Status"),
        new("Events", "EventCount"),
        new("Domains", "DomainCount"),
        new("Storage", "StorageBytes"),
        new("Created", "CreatedAtUtc"),
        new("Subscription Tier", "SubscriptionTier"),
    ];

    internal static readonly List<ColumnDefinition> QuotaColumns =
    [
        new("Max Events/Day", "MaxEventsPerDay"),
        new("Max Storage", "MaxStorageBytes"),
        new("Current Usage", "CurrentUsage"),
    ];

    /// <summary>
    /// Creates the tenant detail subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding)
    {
        Argument<string> tenantIdArg = TenantArguments.TenantId();

        Command command = new("detail", "Show detailed tenant information");
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
            TenantDetail? detail = await client
                .TryGetAsync<TenantDetail>($"api/v1/admin/tenants/{Uri.EscapeDataString(tenantId)}", cancellationToken)
                .ConfigureAwait(false);

            if (detail is null)
            {
                Console.Error.WriteLine($"Tenant '{tenantId}' not found.");
                return ExitCodes.Error;
            }

            string output;
            if (string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase))
            {
                output = formatter.Format(detail);
            }
            else if (string.Equals(options.Format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                var overview = new
                {
                    detail.TenantId,
                    detail.DisplayName,
                    detail.Status,
                    detail.EventCount,
                    detail.DomainCount,
                    detail.StorageBytes,
                    detail.CreatedAtUtc,
                    detail.SubscriptionTier,
                };
                output = formatter.FormatCollection(new[] { overview }, OverviewColumns);
            }
            else
            {
                // Table format — dual-section (overview + quotas)
                var overview = new
                {
                    detail.TenantId,
                    detail.DisplayName,
                    detail.Status,
                    detail.EventCount,
                    detail.DomainCount,
                    detail.StorageBytes,
                    detail.CreatedAtUtc,
                    detail.SubscriptionTier,
                };

                output = formatter.Format(overview, OverviewColumns);

                if (detail.Quotas is not null)
                {
                    var quotaData = new
                    {
                        detail.Quotas.MaxEventsPerDay,
                        detail.Quotas.MaxStorageBytes,
                        detail.Quotas.CurrentUsage,
                    };
                    output += Environment.NewLine + Environment.NewLine
                        + formatter.Format(quotaData, QuotaColumns);
                }
            }

            int writeResult = writer.Write(output);
            return writeResult != ExitCodes.Success ? writeResult : ExitCodes.Success;
        }
        catch (AdminApiException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.Error;
        }
    }
}
