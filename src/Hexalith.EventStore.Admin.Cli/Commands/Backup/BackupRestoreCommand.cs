using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Backup;

/// <summary>
/// The <c>eventstore-admin backup restore</c> subcommand — restores from a backup.
/// </summary>
public static class BackupRestoreCommand
{
    internal static readonly List<ColumnDefinition> ResultColumns =
    [
        new("Operation ID", "OperationId"),
        new("Success", "Success"),
        new("Message", "Message"),
    ];

    /// <summary>
    /// Creates the backup restore subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding)
    {
        Argument<string> backupIdArg = BackupArguments.BackupId();
        Option<DateTimeOffset?> pointInTimeOption = new("--point-in-time") { Description = "Point-in-time for restore (ISO 8601 datetime)" };
        Option<bool> dryRunOption = new("--dry-run") { Description = "Validate restore feasibility without writing" };

        Command command = new("restore", "Restore from a backup");
        command.Arguments.Add(backupIdArg);
        command.Options.Add(pointInTimeOption);
        command.Options.Add(dryRunOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            GlobalOptions options = binding.Resolve(parseResult);
            string backupId = parseResult.GetValue(backupIdArg)!;
            DateTimeOffset? pointInTime = parseResult.GetValue(pointInTimeOption);
            bool dryRun = parseResult.GetValue(dryRunOption);
            return await ExecuteAsync(options, backupId, pointInTime, dryRun, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    internal static async Task<int> ExecuteAsync(
        GlobalOptions options,
        string backupId,
        DateTimeOffset? pointInTime,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        using AdminApiClient client = new(options);
        return await ExecuteAsync(client, options, backupId, pointInTime, dryRun, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string backupId,
        DateTimeOffset? pointInTime,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
        OutputWriter writer = new(options.OutputFile);
        try
        {
            string path = $"api/v1/admin/backups/{Uri.EscapeDataString(backupId)}/restore";
            List<string> queryParams = [];
            if (pointInTime is not null)
            {
                queryParams.Add($"pointInTime={Uri.EscapeDataString(pointInTime.Value.ToString("O"))}");
            }

            queryParams.Add($"dryRun={dryRun.ToString().ToLowerInvariant()}");
            if (queryParams.Count > 0)
            {
                path += "?" + string.Join("&", queryParams);
            }

            AdminOperationResult result = await client
                .PostAsync<AdminOperationResult>(path, cancellationToken)
                .ConfigureAwait(false);

            if (dryRun)
            {
                Console.Error.WriteLine($"Dry-run restore validated. Operation ID: {result.OperationId}");
            }
            else
            {
                Console.Error.WriteLine($"Restore initiated. Operation ID: {result.OperationId}");
            }

            string output;
            if (string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase))
            {
                output = formatter.Format(result);
            }
            else
            {
                output = formatter.Format(result, ResultColumns);
            }

            int writeResult = writer.Write(output);
            return writeResult != ExitCodes.Success ? writeResult : ExitCodes.Success;
        }
        catch (AdminApiException ex)
        {
            string message = ex.HttpStatusCode switch
            {
                404 => $"Backup '{backupId}' not found.",
                _ => ex.Message,
            };
            Console.Error.WriteLine(message);
            return ExitCodes.Error;
        }
    }
}
