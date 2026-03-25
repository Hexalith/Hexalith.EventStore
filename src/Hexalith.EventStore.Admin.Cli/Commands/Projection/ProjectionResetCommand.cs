using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Projection;

/// <summary>
/// The <c>eventstore-admin projection reset</c> subcommand — resets a projection.
/// </summary>
public static class ProjectionResetCommand
{
    /// <summary>
    /// Creates the projection reset subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding)
    {
        Argument<string> tenantArg = ProjectionArguments.Tenant();
        Argument<string> nameArg = ProjectionArguments.Name();
        Option<long?> fromOption = new("--from") { Description = "Position to reset from (omit to reset from beginning)" };

        Command command = new("reset", "Reset a projection to reprocess events");
        command.Arguments.Add(tenantArg);
        command.Arguments.Add(nameArg);
        command.Options.Add(fromOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            GlobalOptions options = binding.Resolve(parseResult);
            string tenant = parseResult.GetValue(tenantArg)!;
            string name = parseResult.GetValue(nameArg)!;
            long? fromPosition = parseResult.GetValue(fromOption);
            return await ExecuteAsync(options, tenant, name, fromPosition, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    internal static async Task<int> ExecuteAsync(
        GlobalOptions options,
        string tenant,
        string name,
        long? fromPosition,
        CancellationToken cancellationToken)
    {
        using AdminApiClient client = new(options);
        return await ExecuteAsync(client, options, tenant, name, fromPosition, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string tenant,
        string name,
        long? fromPosition,
        CancellationToken cancellationToken)
    {
        IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
        OutputWriter writer = new(options.OutputFile);
        try
        {
            string path = $"api/v1/admin/projections/{Uri.EscapeDataString(tenant)}/{Uri.EscapeDataString(name)}/reset";
            AdminOperationResult result = await client
                .PostAsync<AdminOperationResult>(path, new { FromPosition = fromPosition }, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                Console.Error.WriteLine(result.Message ?? "Operation failed.");
                return ExitCodes.Error;
            }

            Console.Error.WriteLine($"Projection '{name}' reset initiated. Operation ID: {result.OperationId}");
            Console.Error.WriteLine($"Check progress: eventstore-admin projection status {tenant} {name}");

            string output = formatter.Format(result);
            int writeResult = writer.Write(output);
            return writeResult != ExitCodes.Success ? writeResult : ExitCodes.Success;
        }
        catch (AdminApiException ex)
        {
            string message = ex.HttpStatusCode switch
            {
                403 => "Access denied. Operator role required to reset projections.",
                404 => $"Projection '{name}' not found in tenant '{tenant}'.",
                _ => ex.Message,
            };
            Console.Error.WriteLine(message);
            return ExitCodes.Error;
        }
    }
}
