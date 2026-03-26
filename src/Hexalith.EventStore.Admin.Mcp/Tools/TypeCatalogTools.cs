namespace Hexalith.EventStore.Admin.Mcp.Tools;

using System.ComponentModel;

using ModelContextProtocol.Server;

/// <summary>
/// MCP tools for discovering domain model type catalog.
/// </summary>
[McpServerToolType]
internal static class TypeCatalogTools
{
    /// <summary>
    /// Discover all registered event types, command types, and aggregate types in the domain model.
    /// </summary>
    [McpServerTool(Name = "types-list")]
    [Description("Discover all registered event types, command types, and aggregate types in the domain model")]
    public static async Task<string> ListTypes(
        AdminApiClient adminApiClient,
        InvestigationSession session,
        [Description("Filter by domain (uses session context if omitted)")] string? domain = null,
        CancellationToken cancellationToken = default)
    {
        domain = NormalizeOptionalScope(domain);
        domain ??= session.GetSnapshot().Domain;

        try
        {
            var eventTypesTask = adminApiClient.ListEventTypesAsync(domain, cancellationToken);
            var commandTypesTask = adminApiClient.ListCommandTypesAsync(domain, cancellationToken);
            var aggregateTypesTask = adminApiClient.ListAggregateTypesAsync(domain, cancellationToken);

            Exception? eventError = null;
            Exception? commandError = null;
            Exception? aggregateError = null;

            try
            {
                await Task.WhenAll(eventTypesTask, commandTypesTask, aggregateTypesTask).ConfigureAwait(false);
            }
            catch
            {
                // Individual tasks may have faulted — handle below
            }

            object? eventTypes = null;
            object? commandTypes = null;
            object? aggregateTypes = null;

            if (eventTypesTask.IsCompletedSuccessfully)
            {
                eventTypes = eventTypesTask.Result;
            }
            else if (eventTypesTask.IsFaulted)
            {
                eventError = eventTypesTask.Exception?.InnerException;
            }

            if (commandTypesTask.IsCompletedSuccessfully)
            {
                commandTypes = commandTypesTask.Result;
            }
            else if (commandTypesTask.IsFaulted)
            {
                commandError = commandTypesTask.Exception?.InnerException;
            }

            if (aggregateTypesTask.IsCompletedSuccessfully)
            {
                aggregateTypes = aggregateTypesTask.Result;
            }
            else if (aggregateTypesTask.IsFaulted)
            {
                aggregateError = aggregateTypesTask.Exception?.InnerException;
            }

            // If all three failed, return a single error
            if (eventError is not null && commandError is not null && aggregateError is not null)
            {
                return ToolHelper.HandleException(eventError);
            }

            return ToolHelper.SerializeResult(new
            {
                eventTypes = eventTypes ?? (object)new { error = true, message = eventError?.Message ?? "Unknown error" },
                commandTypes = commandTypes ?? (object)new { error = true, message = commandError?.Message ?? "Unknown error" },
                aggregateTypes = aggregateTypes ?? (object)new { error = true, message = aggregateError?.Message ?? "Unknown error" },
            });
        }
        catch (Exception ex)
        {
            return ToolHelper.HandleException(ex);
        }
    }

    private static string? NormalizeOptionalScope(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
