using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Stream;

/// <summary>
/// The <c>eventstore-admin stream events</c> subcommand — displays a timeline of commands, events, and queries for a stream.
/// </summary>
public static class StreamEventsCommand {
    internal static readonly List<ColumnDefinition> Columns =
    [
        new("Seq", "SequenceNumber", Align: Alignment.Right),
        new("Timestamp", "Timestamp"),
        new("Type", "EntryType"),
        new("TypeName", "TypeName"),
        new("CorrelationId", "CorrelationId"),
        new("UserId", "UserId"),
    ];

    /// <summary>
    /// Creates the stream events subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding) {
        Argument<string> tenantArg = StreamArguments.Tenant();
        Argument<string> domainArg = StreamArguments.Domain();
        Argument<string> aggregateIdArg = StreamArguments.AggregateId();
        Option<long?> fromOption = new("--from") { Description = "Starting sequence number" };
        Option<long?> toOption = new("--to") { Description = "Ending sequence number" };
        Option<int> countOption = new("--count", "-c") {
            Description = "Maximum number of entries to return",
            DefaultValueFactory = _ => 100
        };

        Command command = new("events", "Browse event timeline for a stream");
        command.Arguments.Add(tenantArg);
        command.Arguments.Add(domainArg);
        command.Arguments.Add(aggregateIdArg);
        command.Options.Add(fromOption);
        command.Options.Add(toOption);
        command.Options.Add(countOption);

        command.SetAction(async (parseResult, cancellationToken) => {
            GlobalOptions options = binding.Resolve(parseResult);
            string tenant = parseResult.GetValue(tenantArg)!;
            string domain = parseResult.GetValue(domainArg)!;
            string aggregateId = parseResult.GetValue(aggregateIdArg)!;
            long? from = parseResult.GetValue(fromOption);
            long? to = parseResult.GetValue(toOption);
            int count = parseResult.GetValue(countOption);
            return await ExecuteAsync(options, tenant, domain, aggregateId, from, to, count, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    internal static async Task<int> ExecuteAsync(
        GlobalOptions options,
        string tenant,
        string domain,
        string aggregateId,
        long? from,
        long? to,
        int count,
        CancellationToken cancellationToken) {
        using AdminApiClient client = new(options);
        return await ExecuteAsync(client, options, tenant, domain, aggregateId, from, to, count, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string tenant,
        string domain,
        string aggregateId,
        long? from,
        long? to,
        int count,
        CancellationToken cancellationToken) {
        IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
        OutputWriter writer = new(options.OutputFile);
        try {
            string basePath = $"api/v1/admin/streams/{Uri.EscapeDataString(tenant)}/{Uri.EscapeDataString(domain)}/{Uri.EscapeDataString(aggregateId)}/timeline";
            List<string> queryParts = [$"count={count}"];
            if (from.HasValue) {
                queryParts.Add($"fromSequence={from.Value}");
            }

            if (to.HasValue) {
                queryParts.Add($"toSequence={to.Value}");
            }

            string path = $"{basePath}?{string.Join("&", queryParts)}";
            PagedResult<TimelineEntry> result = await client
                .GetAsync<PagedResult<TimelineEntry>>(path, cancellationToken)
                .ConfigureAwait(false);

            if (result.Items.Count == 0) {
                Console.Error.WriteLine("No timeline entries found.");
                return ExitCodes.Success;
            }

            if (result.TotalCount > result.Items.Count) {
                Console.Error.WriteLine($"Showing {result.Items.Count} of {result.TotalCount} results.");
            }

            string output;
            if (string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase)) {
                output = formatter.Format(result);
            }
            else {
                output = formatter.FormatCollection(result.Items.ToList(), Columns);
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
