using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Backup;

/// <summary>
/// The <c>eventstore-admin backup trigger</c> subcommand — triggers a backup for a tenant.
/// </summary>
public static class BackupTriggerCommand {
    internal static readonly List<ColumnDefinition> ResultColumns =
    [
        new("Operation ID", "OperationId"),
        new("Success", "Success"),
        new("Message", "Message"),
    ];

    /// <summary>
    /// Creates the backup trigger subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding) {
        Argument<string> tenantIdArg = BackupArguments.TenantId();
        Option<string?> descriptionOption = new("--description", "-d") { Description = "Description for the backup" };
        Option<bool> noSnapshotsOption = new("--no-snapshots") { Description = "Exclude snapshots from backup" };

        Command command = new("trigger", "Trigger a backup for a tenant");
        command.Arguments.Add(tenantIdArg);
        command.Options.Add(descriptionOption);
        command.Options.Add(noSnapshotsOption);

        command.SetAction(async (parseResult, cancellationToken) => {
            GlobalOptions options = binding.Resolve(parseResult);
            string tenantId = parseResult.GetValue(tenantIdArg)!;
            string? description = parseResult.GetValue(descriptionOption);
            bool noSnapshots = parseResult.GetValue(noSnapshotsOption);
            return await ExecuteAsync(options, tenantId, description, noSnapshots, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    internal static async Task<int> ExecuteAsync(
        GlobalOptions options,
        string tenantId,
        string? description,
        bool noSnapshots,
        CancellationToken cancellationToken) {
        using AdminApiClient client = new(options);
        return await ExecuteAsync(client, options, tenantId, description, noSnapshots, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string tenantId,
        string? description,
        bool noSnapshots,
        CancellationToken cancellationToken) {
        IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
        OutputWriter writer = new(options.OutputFile);
        try {
            string path = $"api/v1/admin/backups/{Uri.EscapeDataString(tenantId)}";
            List<string> queryParams = [];
            if (description is not null) {
                queryParams.Add($"description={Uri.EscapeDataString(description)}");
            }

            bool includeSnapshots = !noSnapshots;
            queryParams.Add($"includeSnapshots={includeSnapshots.ToString().ToLowerInvariant()}");
            if (queryParams.Count > 0) {
                path += "?" + string.Join("&", queryParams);
            }

            AdminOperationResult result = await client
                .PostAsync<AdminOperationResult>(path, cancellationToken)
                .ConfigureAwait(false);

            Console.Error.WriteLine($"Backup triggered. Operation ID: {result.OperationId}");

            string output;
            if (string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase)) {
                output = formatter.Format(result);
            }
            else {
                output = formatter.Format(result, ResultColumns);
            }

            int writeResult = writer.Write(output);
            return writeResult != ExitCodes.Success ? writeResult : ExitCodes.Success;
        }
        catch (AdminApiException ex) {
            string message = ex.HttpStatusCode switch {
                403 => "Access denied. Admin role required.",
                _ => ex.Message,
            };
            Console.Error.WriteLine(message);
            return ExitCodes.Error;
        }
    }
}
