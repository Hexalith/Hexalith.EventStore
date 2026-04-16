using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Stream;

/// <summary>
/// The <c>eventstore-admin stream causation</c> subcommand — traces the causation chain for an event.
/// </summary>
public static class StreamCausationCommand {
    internal static readonly List<ColumnDefinition> EventColumns =
    [
        new("Seq", "SequenceNumber", Align: Alignment.Right),
        new("EventType", "EventTypeName"),
        new("Timestamp", "Timestamp"),
    ];

    /// <summary>
    /// Creates the stream causation subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding) {
        Argument<string> tenantArg = StreamArguments.Tenant();
        Argument<string> domainArg = StreamArguments.Domain();
        Argument<string> aggregateIdArg = StreamArguments.AggregateId();
        Option<long> atOption = new("--at", "-a") { Description = "Sequence number to trace", Required = true };

        Command command = new("causation", "Trace causation chain for an event");
        command.Arguments.Add(tenantArg);
        command.Arguments.Add(domainArg);
        command.Arguments.Add(aggregateIdArg);
        command.Options.Add(atOption);

        command.SetAction(async (parseResult, cancellationToken) => {
            GlobalOptions options = binding.Resolve(parseResult);
            string tenant = parseResult.GetValue(tenantArg)!;
            string domain = parseResult.GetValue(domainArg)!;
            string aggregateId = parseResult.GetValue(aggregateIdArg)!;
            long at = parseResult.GetValue(atOption);
            return await ExecuteAsync(options, tenant, domain, aggregateId, at, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    internal static async Task<int> ExecuteAsync(
        GlobalOptions options,
        string tenant,
        string domain,
        string aggregateId,
        long at,
        CancellationToken cancellationToken) {
        using AdminApiClient client = new(options);
        return await ExecuteAsync(client, options, tenant, domain, aggregateId, at, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string tenant,
        string domain,
        string aggregateId,
        long at,
        CancellationToken cancellationToken) {
        IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
        OutputWriter writer = new(options.OutputFile);
        try {
            string path = $"api/v1/admin/streams/{Uri.EscapeDataString(tenant)}/{Uri.EscapeDataString(domain)}/{Uri.EscapeDataString(aggregateId)}/causation?sequenceNumber={at}";
            CausationChain? chain = await client
                .TryGetAsync<CausationChain>(path, cancellationToken)
                .ConfigureAwait(false);

            if (chain is null) {
                Console.Error.WriteLine("Causation chain not found.");
                return ExitCodes.Error;
            }

            string output;
            if (string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase)) {
                output = formatter.Format(chain);
            }
            else if (string.Equals(options.Format, "csv", StringComparison.OrdinalIgnoreCase)) {
                output = formatter.FormatCollection(chain.Events.ToList(), EventColumns);
            }
            else {
                // Table format — dual-section (same pattern as HealthCommand)
                var overview = new {
                    chain.OriginatingCommandType,
                    chain.OriginatingCommandId,
                    chain.CorrelationId,
                    UserId = chain.UserId ?? string.Empty,
                    EventCount = chain.Events.Count,
                    AffectedProjections = string.Join(", ", chain.AffectedProjections),
                };

                output = string.Concat(
                    formatter.Format(overview),
                    Environment.NewLine,
                    Environment.NewLine,
                    formatter.FormatCollection(chain.Events.ToList(), EventColumns));
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
