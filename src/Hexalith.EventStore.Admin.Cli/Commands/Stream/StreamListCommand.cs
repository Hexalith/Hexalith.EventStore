using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Stream;

/// <summary>
/// The <c>eventstore-admin stream list</c> subcommand — lists recently active streams.
/// </summary>
public static class StreamListCommand
{
    internal static readonly List<ColumnDefinition> Columns =
    [
        new("Status", "StreamStatus"),
        new("Tenant", "TenantId"),
        new("Domain", "Domain"),
        new("AggregateId", "AggregateId"),
        new("Events", "EventCount", Align: Alignment.Right),
        new("Last Sequence", "LastEventSequence", Align: Alignment.Right),
        new("Last Activity", "LastActivityUtc"),
        new("Snapshot", "HasSnapshot"),
    ];

    /// <summary>
    /// Creates the stream list subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding)
    {
        Option<string?> tenantOption = new("--tenant", "-T") { Description = "Filter by tenant identifier" };
        Option<string?> domainOption = new("--domain", "-d") { Description = "Filter by domain name" };
        Option<int> countOption = new("--count", "-c") { Description = "Maximum number of streams to return" };
        countOption.DefaultValueFactory = _ => 1000;

        Command command = new("list", "List recently active streams");
        command.Options.Add(tenantOption);
        command.Options.Add(domainOption);
        command.Options.Add(countOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            GlobalOptions options = binding.Resolve(parseResult);
            string? tenant = parseResult.GetValue(tenantOption);
            string? domain = parseResult.GetValue(domainOption);
            int count = parseResult.GetValue(countOption);
            return await ExecuteAsync(options, tenant, domain, count, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    internal static async Task<int> ExecuteAsync(
        GlobalOptions options,
        string? tenant,
        string? domain,
        int count,
        CancellationToken cancellationToken)
    {
        using AdminApiClient client = new(options);
        return await ExecuteAsync(client, options, tenant, domain, count, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string? tenant,
        string? domain,
        int count,
        CancellationToken cancellationToken)
    {
        IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
        OutputWriter writer = new(options.OutputFile);
        try
        {
            List<string> queryParts = [$"count={count}"];
            if (tenant is not null)
            {
                queryParts.Add($"tenantId={Uri.EscapeDataString(tenant)}");
            }

            if (domain is not null)
            {
                queryParts.Add($"domain={Uri.EscapeDataString(domain)}");
            }

            string path = $"api/v1/admin/streams?{string.Join("&", queryParts)}";
            PagedResult<StreamSummary> result = await client
                .GetAsync<PagedResult<StreamSummary>>(path, cancellationToken)
                .ConfigureAwait(false);

            if (result.Items.Count == 0)
            {
                Console.Error.WriteLine("No streams found.");
                return ExitCodes.Success;
            }

            if (result.TotalCount > result.Items.Count)
            {
                Console.Error.WriteLine($"Showing {result.Items.Count} of {result.TotalCount} results.");
            }

            string output;
            if (string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase))
            {
                output = formatter.Format(result);
            }
            else
            {
                output = formatter.FormatCollection(result.Items.ToList(), Columns);
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
