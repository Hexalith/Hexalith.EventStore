using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Snapshot;

/// <summary>
/// The <c>eventstore-admin snapshot set-policy</c> subcommand — sets a snapshot policy.
/// </summary>
public static class SnapshotSetPolicyCommand {
    internal static readonly List<ColumnDefinition> ResultColumns =
    [
        new("Operation ID", "OperationId"),
        new("Success", "Success"),
        new("Message", "Message"),
    ];

    /// <summary>
    /// Creates the snapshot set-policy subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding) {
        Argument<string> tenantIdArg = SnapshotArguments.TenantId();
        Argument<string> domainArg = SnapshotArguments.Domain();
        Argument<string> aggregateTypeArg = SnapshotArguments.AggregateType();
        Argument<int> intervalEventsArg = new("intervalEvents") { Description = "Number of events between automatic snapshots" };

        Command command = new("set-policy", "Set a snapshot policy for an aggregate type");
        command.Arguments.Add(tenantIdArg);
        command.Arguments.Add(domainArg);
        command.Arguments.Add(aggregateTypeArg);
        command.Arguments.Add(intervalEventsArg);

        command.SetAction(async (parseResult, cancellationToken) => {
            GlobalOptions options = binding.Resolve(parseResult);
            string tenantId = parseResult.GetValue(tenantIdArg)!;
            string domain = parseResult.GetValue(domainArg)!;
            string aggregateType = parseResult.GetValue(aggregateTypeArg)!;
            int intervalEvents = parseResult.GetValue(intervalEventsArg);
            return await ExecuteAsync(options, tenantId, domain, aggregateType, intervalEvents, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    internal static async Task<int> ExecuteAsync(
        GlobalOptions options,
        string tenantId,
        string domain,
        string aggregateType,
        int intervalEvents,
        CancellationToken cancellationToken) {
        using AdminApiClient client = new(options);
        return await ExecuteAsync(client, options, tenantId, domain, aggregateType, intervalEvents, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string tenantId,
        string domain,
        string aggregateType,
        int intervalEvents,
        CancellationToken cancellationToken) {
        if (intervalEvents <= 0) {
            Console.Error.WriteLine("intervalEvents must be a positive integer.");
            return ExitCodes.Error;
        }

        IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
        OutputWriter writer = new(options.OutputFile);
        try {
            string path = $"api/v1/admin/storage/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(domain)}/{Uri.EscapeDataString(aggregateType)}/snapshot-policy?intervalEvents={intervalEvents}";
            AdminOperationResult result = await client
                .PutAsync<AdminOperationResult>(path, cancellationToken)
                .ConfigureAwait(false);

            Console.Error.WriteLine($"Snapshot policy set. Operation ID: {result.OperationId}");

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
                _ => ex.Message,
            };
            Console.Error.WriteLine(message);
            return ExitCodes.Error;
        }
    }
}
