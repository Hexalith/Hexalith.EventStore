using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Tenant;

/// <summary>
/// The <c>eventstore-admin tenant verify</c> subcommand — validates tenant health.
/// </summary>
public static class TenantVerifyCommand
{
    internal static readonly List<ColumnDefinition> Columns =
    [
        new("Tenant ID", "TenantId"),
        new("Name", "Name"),
        new("Status", "Status"),
        new("Created", "CreatedAt"),
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

        Command command = new("verify", "Verify tenant health");
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
            TenantDetail? detail = await client
                .TryGetAsync<TenantDetail>($"api/v1/admin/tenants/{Uri.EscapeDataString(tenantId)}", cancellationToken)
                .ConfigureAwait(false);

            if (detail is null)
            {
                Console.Error.WriteLine($"Tenant '{tenantId}' not found.");
                return ExitCodes.Error;
            }

            string verdict = DeriveVerdict(detail.Status);
            int exitCode = MapExitCode(verdict);

            if (!quiet || options.OutputFile is not null)
            {
                TenantVerifyResult result = new(
                    detail.TenantId,
                    detail.Name,
                    detail.Status,
                    detail.CreatedAt,
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

    internal static string DeriveVerdict(TenantStatusType status) =>
        status switch
        {
            TenantStatusType.Disabled => "FAIL",
            _ => "PASS",
        };

    internal static int MapExitCode(string verdict) => verdict switch
    {
        "PASS" => ExitCodes.Success,
        _ => ExitCodes.Error,
    };

    internal record TenantVerifyResult(
        string TenantId,
        string Name,
        TenantStatusType Status,
        DateTimeOffset CreatedAt,
        string Verdict);
}
