using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Stream;

/// <summary>
/// The <c>eventstore-admin stream diff</c> subcommand — shows state changes between two sequence positions.
/// </summary>
public static class StreamDiffCommand
{
    internal static readonly List<ColumnDefinition> Columns =
    [
        new("FieldPath", "FieldPath"),
        new("OldValue", "OldValue"),
        new("NewValue", "NewValue"),
    ];

    /// <summary>
    /// Creates the stream diff subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding)
    {
        Argument<string> tenantArg = StreamArguments.Tenant();
        Argument<string> domainArg = StreamArguments.Domain();
        Argument<string> aggregateIdArg = StreamArguments.AggregateId();
        Option<long> fromOption = new("--from") { Description = "Starting sequence number", Required = true };
        Option<long> toOption = new("--to") { Description = "Ending sequence number", Required = true };

        Command command = new("diff", "Diff aggregate state between two sequence positions");
        command.Arguments.Add(tenantArg);
        command.Arguments.Add(domainArg);
        command.Arguments.Add(aggregateIdArg);
        command.Options.Add(fromOption);
        command.Options.Add(toOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            GlobalOptions options = binding.Resolve(parseResult);
            string tenant = parseResult.GetValue(tenantArg)!;
            string domain = parseResult.GetValue(domainArg)!;
            string aggregateId = parseResult.GetValue(aggregateIdArg)!;
            long from = parseResult.GetValue(fromOption);
            long to = parseResult.GetValue(toOption);
            return await ExecuteAsync(options, tenant, domain, aggregateId, from, to, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    internal static async Task<int> ExecuteAsync(
        GlobalOptions options,
        string tenant,
        string domain,
        string aggregateId,
        long from,
        long to,
        CancellationToken cancellationToken)
    {
        using AdminApiClient client = new(options);
        return await ExecuteAsync(client, options, tenant, domain, aggregateId, from, to, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string tenant,
        string domain,
        string aggregateId,
        long from,
        long to,
        CancellationToken cancellationToken)
    {
        IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
        OutputWriter writer = new(options.OutputFile);
        try
        {
            string path = $"api/v1/admin/streams/{Uri.EscapeDataString(tenant)}/{Uri.EscapeDataString(domain)}/{Uri.EscapeDataString(aggregateId)}/diff?fromSequence={from}&toSequence={to}";
            AggregateStateDiff? diff = await client
                .TryGetAsync<AggregateStateDiff>(path, cancellationToken)
                .ConfigureAwait(false);

            if (diff is null)
            {
                Console.Error.WriteLine("Aggregate state not found for the specified range.");
                return ExitCodes.Error;
            }

            if (diff.ChangedFields is null || diff.ChangedFields.Count == 0)
            {
                Console.Error.WriteLine($"No changes between sequence {from} and {to}.");
                return ExitCodes.Success;
            }

            string output;
            if (string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase))
            {
                output = formatter.Format(diff);
            }
            else
            {
                Console.Error.WriteLine($"Diff from sequence {from} to {to}");
                output = formatter.FormatCollection(diff.ChangedFields.ToList(), Columns);
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
