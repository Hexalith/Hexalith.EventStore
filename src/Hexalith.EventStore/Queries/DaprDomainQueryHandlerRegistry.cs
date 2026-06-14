using System.Collections.Concurrent;

using Dapr.Client;

using Hexalith.EventStore.Server.Commands;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Queries;

/// <summary>
/// Reads each domain's handler-served query types from the admin operational index (DAPR state key
/// <c>admin:query-types:{domain}</c>, written at startup by <c>AdminOperationalIndexHostedService</c>),
/// with a short in-memory cache mirroring <c>DaprCommandAggregateTypeResolver</c>. On any read failure it
/// reports "no handler", so routing fails safe to the projection-actor path.
/// </summary>
public sealed class DaprDomainQueryHandlerRegistry(
    DaprClient daprClient,
    IOptions<CommandStatusOptions> commandStatusOptions,
    ILogger<DaprDomainQueryHandlerRegistry> logger) : IDomainQueryHandlerRegistry {
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private readonly string _stateStoreName = commandStatusOptions.Value.StateStoreName;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public async Task<bool> SupportsQueryAsync(string domain, string queryType, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryType);

        IReadOnlyCollection<string> queryTypes = await GetQueryTypesAsync(domain, cancellationToken).ConfigureAwait(false);
        return queryTypes.Contains(queryType, StringComparer.Ordinal);
    }

    private async Task<IReadOnlyCollection<string>> GetQueryTypesAsync(string domain, CancellationToken cancellationToken) {
        if (_cache.TryGetValue(domain, out CacheEntry? cached) && cached.ExpiresAtUtc > DateTimeOffset.UtcNow) {
            return cached.QueryTypes;
        }

        IReadOnlyCollection<string> queryTypes;
        try {
            List<string>? stored = await daprClient
                .GetStateAsync<List<string>>(_stateStoreName, $"admin:query-types:{domain}", cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            queryTypes = stored is null ? [] : new HashSet<string>(stored, StringComparer.Ordinal);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            logger.LogDebug(
                ex,
                "Failed to read handler query types for domain '{Domain}'; treating as no handler-based queries.",
                domain);
            queryTypes = [];
        }

        _cache[domain] = new CacheEntry(queryTypes, DateTimeOffset.UtcNow.Add(CacheTtl));
        return queryTypes;
    }

    private sealed record CacheEntry(IReadOnlyCollection<string> QueryTypes, DateTimeOffset ExpiresAtUtc);
}
