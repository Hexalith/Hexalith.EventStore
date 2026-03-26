namespace Hexalith.EventStore.Admin.Mcp;

using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;

/// <summary>
/// AdminApiClient partial — type catalog query methods.
/// </summary>
internal sealed partial class AdminApiClient
{
    /// <summary>
    /// Lists all registered event types, optionally filtered by domain.
    /// </summary>
    public async Task<IReadOnlyList<EventTypeInfo>> ListEventTypesAsync(
        string? domain,
        CancellationToken cancellationToken)
    {
        string path = "/api/v1/admin/types/events";
        if (!string.IsNullOrEmpty(domain))
        {
            path += $"?domain={Uri.EscapeDataString(domain)}";
        }

        return await GetListAsync<EventTypeInfo>(path, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lists all registered command types, optionally filtered by domain.
    /// </summary>
    public async Task<IReadOnlyList<CommandTypeInfo>> ListCommandTypesAsync(
        string? domain,
        CancellationToken cancellationToken)
    {
        string path = "/api/v1/admin/types/commands";
        if (!string.IsNullOrEmpty(domain))
        {
            path += $"?domain={Uri.EscapeDataString(domain)}";
        }

        return await GetListAsync<CommandTypeInfo>(path, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lists all registered aggregate types, optionally filtered by domain.
    /// </summary>
    public async Task<IReadOnlyList<AggregateTypeInfo>> ListAggregateTypesAsync(
        string? domain,
        CancellationToken cancellationToken)
    {
        string path = "/api/v1/admin/types/aggregates";
        if (!string.IsNullOrEmpty(domain))
        {
            path += $"?domain={Uri.EscapeDataString(domain)}";
        }

        return await GetListAsync<AggregateTypeInfo>(path, cancellationToken).ConfigureAwait(false);
    }
}
