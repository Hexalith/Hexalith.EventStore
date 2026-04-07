using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Backup;

/// <summary>
/// The <c>eventstore-admin backup import-stream</c> subcommand — imports a stream from a file.
/// </summary>
public static class BackupImportStreamCommand
{
    internal static readonly List<ColumnDefinition> ResultColumns =
    [
        new("Operation ID", "OperationId"),
        new("Success", "Success"),
        new("Message", "Message"),
    ];

    /// <summary>
    /// Creates the backup import-stream subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding)
    {
        Argument<string> tenantIdArg = BackupArguments.TenantId();
        Option<string> fileOption = new("--file", "-f") { Description = "Path to the JSON file to import", Required = true };

        Command command = new("import-stream", "Import a stream from a JSON file");
        command.Arguments.Add(tenantIdArg);
        command.Options.Add(fileOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            GlobalOptions options = binding.Resolve(parseResult);
            string tenantId = parseResult.GetValue(tenantIdArg)!;
            string filePath = parseResult.GetValue(fileOption)!;
            return await ExecuteAsync(options, tenantId, filePath, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    internal static async Task<int> ExecuteAsync(
        GlobalOptions options,
        string tenantId,
        string filePath,
        CancellationToken cancellationToken)
    {
        using AdminApiClient client = new(options);
        return await ExecuteAsync(client, options, tenantId, filePath, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string tenantId,
        string filePath,
        CancellationToken cancellationToken)
    {
        IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
        OutputWriter writer = new(options.OutputFile);

        if (!File.Exists(filePath))
        {
            await Console.Error.WriteLineAsync($"File not found: {filePath}").ConfigureAwait(false);
            return ExitCodes.Error;
        }

        try
        {
            string content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            string path = $"api/v1/admin/backups/import-stream?tenantId={Uri.EscapeDataString(tenantId)}";
            AdminOperationResult result = await client
                .PostAsync<AdminOperationResult>(path, content, cancellationToken)
                .ConfigureAwait(false);

            Console.Error.WriteLine($"Stream imported. Operation ID: {result.OperationId}");

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
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.Error;
        }
    }
}
