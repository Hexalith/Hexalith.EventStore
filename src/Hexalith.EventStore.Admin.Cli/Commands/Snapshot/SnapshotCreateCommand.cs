using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Snapshot;

/// <summary>
/// The <c>eventstore-admin snapshot create</c> subcommand — creates an on-demand snapshot.
/// </summary>
public static class SnapshotCreateCommand {
    internal static readonly List<ColumnDefinition> ResultColumns =
    [
        new("Operation ID", "OperationId"),
        new("Success", "Success"),
        new("Message", "Message"),
    ];

    /// <summary>
    /// Creates the snapshot create subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding) {
        Argument<string> tenantIdArg = SnapshotArguments.TenantId();
        Argument<string> domainArg = SnapshotArguments.Domain();
        Argument<string> aggregateIdArg = SnapshotArguments.AggregateId();

        Command command = new("create", "Create an on-demand snapshot for an aggregate");
        command.Arguments.Add(tenantIdArg);
        command.Arguments.Add(domainArg);
        command.Arguments.Add(aggregateIdArg);

        command.SetAction(async (parseResult, cancellationToken) => {
            GlobalOptions options = binding.Resolve(parseResult);
            string tenantId = parseResult.GetValue(tenantIdArg)!;
            string domain = parseResult.GetValue(domainArg)!;
            string aggregateId = parseResult.GetValue(aggregateIdArg)!;
            return await ExecuteAsync(options, tenantId, domain, aggregateId, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    internal static async Task<int> ExecuteAsync(
        GlobalOptions options,
        string tenantId,
        string domain,
        string aggregateId,
        CancellationToken cancellationToken) {
        using AdminApiClient client = new(options);
        return await ExecuteAsync(client, options, tenantId, domain, aggregateId, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string tenantId,
        string domain,
        string aggregateId,
        CancellationToken cancellationToken) {
        IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
        OutputWriter writer = new(options.OutputFile);
        try {
            string path = $"api/v1/admin/storage/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(domain)}/{Uri.EscapeDataString(aggregateId)}/snapshot";
            AdminOperationResult result = await client
                .PostAsync<AdminOperationResult>(path, cancellationToken)
                .ConfigureAwait(false);

            Console.Error.WriteLine($"Snapshot created. Operation ID: {result.OperationId}");

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
                403 => "Access denied. Operator role required.",
                404 => "Aggregate not found.",
                _ => ex.Message,
            };
            Console.Error.WriteLine(message);
            return ExitCodes.Error;
        }
    }
}
