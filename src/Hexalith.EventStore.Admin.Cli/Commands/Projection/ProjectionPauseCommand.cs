using System.CommandLine;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Projection;

/// <summary>
/// The <c>eventstore-admin projection pause</c> subcommand — pauses a projection.
/// </summary>
public static class ProjectionPauseCommand {
    /// <summary>
    /// Creates the projection pause subcommand wired to the shared global options.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding) {
        Argument<string> tenantArg = ProjectionArguments.Tenant();
        Argument<string> nameArg = ProjectionArguments.Name();

        Command command = new("pause", "Pause a projection");
        command.Arguments.Add(tenantArg);
        command.Arguments.Add(nameArg);

        command.SetAction(async (parseResult, cancellationToken) => {
            GlobalOptions options = binding.Resolve(parseResult);
            string tenant = parseResult.GetValue(tenantArg)!;
            string name = parseResult.GetValue(nameArg)!;
            return await ExecuteAsync(options, tenant, name, cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    internal static async Task<int> ExecuteAsync(
        GlobalOptions options,
        string tenant,
        string name,
        CancellationToken cancellationToken) {
        using AdminApiClient client = new(options);
        return await ExecuteAsync(client, options, tenant, name, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string tenant,
        string name,
        CancellationToken cancellationToken) {
        IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
        OutputWriter writer = new(options.OutputFile);
        try {
            string path = $"api/v1/admin/projections/{Uri.EscapeDataString(tenant)}/{Uri.EscapeDataString(name)}/pause";
            AdminOperationResult result = await client
                .PostAsync<AdminOperationResult>(path, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success) {
                Console.Error.WriteLine(result.Message ?? "Operation failed.");
                return ExitCodes.Error;
            }

            Console.Error.WriteLine($"Projection '{name}' paused successfully. Operation ID: {result.OperationId}");

            string output = formatter.Format(result);
            int writeResult = writer.Write(output);
            return writeResult != ExitCodes.Success ? writeResult : ExitCodes.Success;
        }
        catch (AdminApiException ex) {
            string message = ex.HttpStatusCode switch {
                403 => "Access denied. Operator role required to pause projections.",
                404 => $"Projection '{name}' not found in tenant '{tenant}'.",
                _ => ex.Message,
            };
            Console.Error.WriteLine(message);
            return ExitCodes.Error;
        }
    }
}
