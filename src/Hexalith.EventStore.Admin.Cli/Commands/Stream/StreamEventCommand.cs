using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Stream;

/// <summary>
/// The <c>eventstore-admin stream event</c> subcommand — displays detail for a single event.
/// </summary>
public static class StreamEventCommand {
    internal static readonly List<ColumnDefinition> Columns =
    [
        new("Tenant", "TenantId"),
        new("Domain", "Domain"),
        new("AggregateId", "AggregateId"),
        new("Seq", "SequenceNumber"),
        new("EventType", "EventTypeName"),
        new("Timestamp", "Timestamp"),
        new("CorrelationId", "CorrelationId"),
        new("CausationId", "CausationId"),
        new("UserId", "UserId"),
        new("PayloadJson", "PayloadJson", MaxWidth: 80),
    ];

    /// <summary>
    /// Creates the stream event subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding) {
        Argument<string> tenantArg = StreamArguments.Tenant();
        Argument<string> domainArg = StreamArguments.Domain();
        Argument<string> aggregateIdArg = StreamArguments.AggregateId();
        Argument<long> seqArg = new("sequenceNumber") { Description = "Event sequence number" };

        Command command = new("event", "Inspect a single event by sequence number");
        command.Arguments.Add(tenantArg);
        command.Arguments.Add(domainArg);
        command.Arguments.Add(aggregateIdArg);
        command.Arguments.Add(seqArg);

        command.SetAction(async (parseResult, cancellationToken) => {
            GlobalOptions options = binding.Resolve(parseResult);
            string tenant = parseResult.GetValue(tenantArg)!;
            string domain = parseResult.GetValue(domainArg)!;
            string aggregateId = parseResult.GetValue(aggregateIdArg)!;
            long seq = parseResult.GetValue(seqArg);
            return await ExecuteAsync(options, tenant, domain, aggregateId, seq, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    internal static async Task<int> ExecuteAsync(
        GlobalOptions options,
        string tenant,
        string domain,
        string aggregateId,
        long sequenceNumber,
        CancellationToken cancellationToken) {
        using AdminApiClient client = new(options);
        return await ExecuteAsync(client, options, tenant, domain, aggregateId, sequenceNumber, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string tenant,
        string domain,
        string aggregateId,
        long sequenceNumber,
        CancellationToken cancellationToken) {
        IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
        OutputWriter writer = new(options.OutputFile);
        try {
            string path = $"api/v1/admin/streams/{Uri.EscapeDataString(tenant)}/{Uri.EscapeDataString(domain)}/{Uri.EscapeDataString(aggregateId)}/events/{sequenceNumber}";
            EventDetail? detail = await client
                .TryGetAsync<EventDetail>(path, cancellationToken)
                .ConfigureAwait(false);

            if (detail is null) {
                Console.Error.WriteLine($"Event not found at sequence {sequenceNumber} in stream {tenant}:{domain}:{aggregateId}.");
                return ExitCodes.Error;
            }

            string output;
            if (string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase)) {
                output = formatter.Format(detail);
            }
            else {
                output = formatter.Format(detail, Columns);
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
