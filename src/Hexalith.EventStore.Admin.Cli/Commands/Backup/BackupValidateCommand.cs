using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Backup;

/// <summary>
/// The <c>eventstore-admin backup validate</c> subcommand — validates a backup's integrity.
/// </summary>
public static class BackupValidateCommand
{
    internal static readonly List<ColumnDefinition> ResultColumns =
    [
        new("Operation ID", "OperationId"),
        new("Success", "Success"),
        new("Message", "Message"),
    ];

    /// <summary>
    /// Creates the backup validate subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding)
    {
        Argument<string> backupIdArg = BackupArguments.BackupId();

        Command command = new("validate", "Validate a backup's integrity");
        command.Arguments.Add(backupIdArg);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            GlobalOptions options = binding.Resolve(parseResult);
            string backupId = parseResult.GetValue(backupIdArg)!;
            return await ExecuteAsync(options, backupId, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    internal static async Task<int> ExecuteAsync(
        GlobalOptions options,
        string backupId,
        CancellationToken cancellationToken)
    {
        using AdminApiClient client = new(options);
        return await ExecuteAsync(client, options, backupId, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string backupId,
        CancellationToken cancellationToken)
    {
        IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
        OutputWriter writer = new(options.OutputFile);
        try
        {
            string path = $"api/v1/admin/backups/{Uri.EscapeDataString(backupId)}/validate";
            AdminOperationResult result = await client
                .PostAsync<AdminOperationResult>(path, cancellationToken)
                .ConfigureAwait(false);

            Console.Error.WriteLine($"Backup validation started. Operation ID: {result.OperationId}");

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
