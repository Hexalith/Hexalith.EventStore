using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// DAPR-backed implementation of <see cref="ITypeCatalogService"/>.
/// Reads type catalog indexes from the state store. Indexes are populated by the event publication pipeline.
/// </summary>
public sealed class DaprTypeCatalogService : ITypeCatalogService
{
    private readonly DaprClient _daprClient;
    private readonly ILogger<DaprTypeCatalogService> _logger;
    private readonly AdminServerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprTypeCatalogService"/> class.
    /// </summary>
    /// <param name="daprClient">The DAPR client.</param>
    /// <param name="options">The admin server options.</param>
    /// <param name="logger">The logger.</param>
    public DaprTypeCatalogService(
        DaprClient daprClient,
        IOptions<AdminServerOptions> options,
        ILogger<DaprTypeCatalogService> logger)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _daprClient = daprClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EventTypeInfo>> ListEventTypesAsync(
        string? domain,
        CancellationToken ct = default)
    {
        string indexKey = $"admin:type-catalog:events:{domain ?? "all"}";
        return await ReadCatalogIndexAsync<EventTypeInfo>(indexKey, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CommandTypeInfo>> ListCommandTypesAsync(
        string? domain,
        CancellationToken ct = default)
    {
        string indexKey = $"admin:type-catalog:commands:{domain ?? "all"}";
        return await ReadCatalogIndexAsync<CommandTypeInfo>(indexKey, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AggregateTypeInfo>> ListAggregateTypesAsync(
        string? domain,
        CancellationToken ct = default)
    {
        string indexKey = $"admin:type-catalog:aggregates:{domain ?? "all"}";
        return await ReadCatalogIndexAsync<AggregateTypeInfo>(indexKey, ct).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<T>> ReadCatalogIndexAsync<T>(string indexKey, CancellationToken ct)
    {
        List<T>? result = await _daprClient
            .GetStateAsync<List<T>>(_options.StateStoreName, indexKey, cancellationToken: ct)
            .ConfigureAwait(false);

        if (result is null)
        {
            _logger.LogWarning("Admin index '{IndexKey}' not found. Index population requires admin projection setup.", indexKey);
            return [];
        }

        return result;
    }
}
