using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Backup;

/// <summary>
/// The <c>eventstore-admin backup export-stream</c> subcommand — exports a stream.
/// </summary>
public static class BackupExportStreamCommand {
    internal static readonly List<ColumnDefinition> Columns =
    [
        new("Success", "Success"),
        new("Tenant", "TenantId"),
        new("Domain", "Domain"),
        new("Aggregate", "AggregateId"),
        new("Events", "EventCount", Align: Alignment.Right),
        new("File Name", "FileName"),
    ];

    /// <summary>
    /// Creates the backup export-stream subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding) {
        Argument<string> tenantIdArg = BackupArguments.TenantId();
        Argument<string> domainArg = BackupArguments.Domain();
        Argument<string> aggregateIdArg = BackupArguments.AggregateId();
        Option<string> exportFormatOption = new("--export-format") {
            Description = "Export format: JSON or CloudEvents",
            DefaultValueFactory = _ => "JSON"
        };

        Command command = new("export-stream", "Export a single stream");
        command.Arguments.Add(tenantIdArg);
        command.Arguments.Add(domainArg);
        command.Arguments.Add(aggregateIdArg);
        command.Options.Add(exportFormatOption);

        command.SetAction(async (parseResult, cancellationToken) => {
            GlobalOptions options = binding.Resolve(parseResult);
            string tenantId = parseResult.GetValue(tenantIdArg)!;
            string domain = parseResult.GetValue(domainArg)!;
            string aggregateId = parseResult.GetValue(aggregateIdArg)!;
            string exportFormat = parseResult.GetValue(exportFormatOption)!;
            return await ExecuteAsync(options, tenantId, domain, aggregateId, exportFormat, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    internal static async Task<int> ExecuteAsync(
        GlobalOptions options,
        string tenantId,
        string domain,
        string aggregateId,
        string exportFormat,
        CancellationToken cancellationToken) {
        using AdminApiClient client = new(options);
        return await ExecuteAsync(client, options, tenantId, domain, aggregateId, exportFormat, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string tenantId,
        string domain,
        string aggregateId,
        string exportFormat,
        CancellationToken cancellationToken) {
        IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
        OutputWriter writer = new(options.OutputFile);
        try {
            StreamExportRequest request = new(tenantId, domain, aggregateId, exportFormat);
            StreamExportResult result = await client
                .PostAsync<StreamExportResult>("api/v1/admin/backups/export-stream", request, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success) {
                Console.Error.WriteLine(result.ErrorMessage ?? "Export failed.");
                return ExitCodes.Error;
            }

            string output;
            if (string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase)) {
                output = formatter.Format(result);
            }
            else {
                output = formatter.Format(result, Columns);
            }

            int writeResult = writer.Write(output);
            return writeResult != ExitCodes.Success ? writeResult : ExitCodes.Success;
        }
        catch (AdminApiException ex) {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.Error;
        }
    }
}
