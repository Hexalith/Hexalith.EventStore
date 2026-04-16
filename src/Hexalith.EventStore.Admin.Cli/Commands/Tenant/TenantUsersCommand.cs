using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Tenant;

/// <summary>
/// The <c>eventstore-admin tenant users</c> subcommand — lists users assigned to a tenant.
/// </summary>
public static class TenantUsersCommand {
    internal static readonly List<ColumnDefinition> Columns =
    [
        new("User ID", "UserId"),
        new("Role", "Role"),
    ];

    /// <summary>
    /// Creates the tenant users subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding) {
        Argument<string> tenantIdArg = TenantArguments.TenantId();

        Command command = new("users", "List users assigned to a tenant");
        command.Arguments.Add(tenantIdArg);

        command.SetAction(async (parseResult, cancellationToken) => {
            GlobalOptions options = binding.Resolve(parseResult);
            string tenantId = parseResult.GetValue(tenantIdArg)!;
            return await ExecuteAsync(options, tenantId, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    internal static async Task<int> ExecuteAsync(
        GlobalOptions options,
        string tenantId,
        CancellationToken cancellationToken) {
        using AdminApiClient client = new(options);
        return await ExecuteAsync(client, options, tenantId, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string tenantId,
        CancellationToken cancellationToken) {
        IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
        OutputWriter writer = new(options.OutputFile);
        try {
            List<TenantUser>? result = await client
                .TryGetAsync<List<TenantUser>>($"api/v1/admin/tenants/{Uri.EscapeDataString(tenantId)}/users", cancellationToken)
                .ConfigureAwait(false);

            if (result is null) {
                Console.Error.WriteLine($"Tenant '{tenantId}' not found.");
                return ExitCodes.Error;
            }

            if (result.Count == 0) {
                Console.Error.WriteLine($"No users found for tenant '{tenantId}'.");
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
