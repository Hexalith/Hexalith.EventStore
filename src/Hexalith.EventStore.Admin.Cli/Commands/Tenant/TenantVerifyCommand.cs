using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Tenant;

/// <summary>
/// The <c>eventstore-admin tenant verify</c> subcommand — validates tenant health and quota compliance.
/// </summary>
public static class TenantVerifyCommand
{
    internal static readonly List<ColumnDefinition> Columns =
    [
        new("Tenant ID", "TenantId"),
        new("Status", "Status"),
        new("Subscription Tier", "SubscriptionTier"),
        new("Events", "EventCount"),
        new("Storage", "StorageBytes"),
        new("Max Events/Day", "MaxEventsPerDay"),
        new("Max Storage", "MaxStorageBytes"),
        new("Current Usage", "CurrentUsage"),
        new("Usage %", "UsagePercentage"),
        new("Verdict", "Verdict"),
    ];

    /// <summary>
    /// Creates the tenant verify subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding)
    {
        Argument<string> tenantIdArg = TenantArguments.TenantId();

        Option<bool> quietOption = new("--quiet", "-q") { Description = "Suppress stdout output, only return exit code" };
        quietOption.DefaultValueFactory = _ => false;

        Command command = new("verify", "Verify tenant health and quota compliance");
        command.Arguments.Add(tenantIdArg);
        command.Options.Add(quietOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            GlobalOptions options = binding.Resolve(parseResult);
            string tenantId = parseResult.GetValue(tenantIdArg)!;
            bool quiet = parseResult.GetValue(quietOption);
            return await ExecuteAsync(options, tenantId, quiet, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    internal static async Task<int> ExecuteAsync(
        GlobalOptions options,
        string tenantId,
        bool quiet,
        CancellationToken cancellationToken)
    {
        using AdminApiClient client = new(options);
        return await ExecuteAsync(client, options, tenantId, quiet, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string tenantId,
        bool quiet,
        CancellationToken cancellationToken)
    {
        try
        {
            // Step 1: Get tenant detail
            TenantDetail? detail = await client
                .TryGetAsync<TenantDetail>($"api/v1/admin/tenants/{Uri.EscapeDataString(tenantId)}", cancellationToken)
                .ConfigureAwait(false);

            if (detail is null)
            {
                Console.Error.WriteLine($"Tenant '{tenantId}' not found.");
                return ExitCodes.Error;
            }

            // Step 2: Get quotas
            TenantQuotas? quotas = await client
                .TryGetAsync<TenantQuotas>($"api/v1/admin/tenants/{Uri.EscapeDataString(tenantId)}/quotas", cancellationToken)
                .ConfigureAwait(false);

            // Step 3: Calculate verdict
            int usagePct = quotas is not null
                ? (int)(quotas.CurrentUsage * 100 / Math.Max(quotas.MaxStorageBytes, 1))
                : 0;
            string verdict = DeriveVerdict(detail.Status, usagePct);
            int exitCode = MapExitCode(verdict);

            // Step 4: Output
            if (!quiet || options.OutputFile is not null)
            {
                TenantVerifyResult result = new(
                    detail.TenantId,
                    detail.DisplayName,
                    detail.Status,
                    detail.SubscriptionTier,
                    detail.EventCount,
                    detail.StorageBytes,
                    quotas?.MaxEventsPerDay ?? 0,
                    quotas?.MaxStorageBytes ?? 0,
                    quotas?.CurrentUsage ?? 0,
                    usagePct,
                    verdict);

                IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
                string output;
                if (string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase))
                {
                    output = formatter.Format(result);
                }
                else
                {
                    output = formatter.Format(result, Columns);
                }

                OutputWriter writer = new(options.OutputFile);
                int writeResult = writer.Write(output);
                if (writeResult != ExitCodes.Success)
                {
                    return writeResult;
                }
            }

            return exitCode;
        }
        catch (AdminApiException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.Error;
        }
    }

    internal static string DeriveVerdict(TenantStatusType status, int usagePercentage) =>
        status switch
        {
            TenantStatusType.Suspended => "FAIL",
            TenantStatusType.Onboarding => "FAIL",
            _ when usagePercentage >= 100 => "FAIL",
            _ when usagePercentage >= 90 => "WARNING",
            _ => "PASS",
        };

    internal static int MapExitCode(string verdict) => verdict switch
    {
        "PASS" => ExitCodes.Success,
        "WARNING" => ExitCodes.Degraded,
        _ => ExitCodes.Error,
    };

    internal record TenantVerifyResult(
        string TenantId,
        string DisplayName,
        TenantStatusType Status,
        string? SubscriptionTier,
        long EventCount,
        long StorageBytes,
        long MaxEventsPerDay,
        long MaxStorageBytes,
        long CurrentUsage,
        int UsagePercentage,
        string Verdict);
}
