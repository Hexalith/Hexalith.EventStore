using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Stream;

/// <summary>
/// The <c>eventstore-admin stream state</c> subcommand — reconstructs aggregate state at a sequence position.
/// </summary>
public static class StreamStateCommand {
    internal static readonly List<ColumnDefinition> Columns =
    [
        new("Tenant", "TenantId"),
        new("Domain", "Domain"),
        new("AggregateId", "AggregateId"),
        new("Seq", "SequenceNumber"),
        new("Timestamp", "Timestamp"),
        new("StateJson", "StateJson", MaxWidth: 80),
    ];

    /// <summary>
    /// Creates the stream state subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding) {
        Argument<string> tenantArg = StreamArguments.Tenant();
        Argument<string> domainArg = StreamArguments.Domain();
        Argument<string> aggregateIdArg = StreamArguments.AggregateId();
        Option<long> atOption = new("--at", "-a") { Description = "Sequence number to inspect", Required = true };

        Command command = new("state", "Reconstruct aggregate state at a sequence position");
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
            string path = $"api/v1/admin/streams/{Uri.EscapeDataString(tenant)}/{Uri.EscapeDataString(domain)}/{Uri.EscapeDataString(aggregateId)}/state?sequenceNumber={at}";
            AggregateStateSnapshot? snapshot = await client
                .TryGetAsync<AggregateStateSnapshot>(path, cancellationToken)
                .ConfigureAwait(false);

            if (snapshot is null) {
                Console.Error.WriteLine($"Aggregate state not found at sequence {at}.");
                return ExitCodes.Error;
            }

            string output;
            if (string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase)) {
                output = formatter.Format(snapshot);
            }
            else {
                output = formatter.Format(snapshot, Columns);
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
