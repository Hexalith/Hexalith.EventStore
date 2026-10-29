using System.Collections.Concurrent;

using Dapr.Client;

using Hexalith.EventStore.Server.Commands;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Queries;

/// <summary>
/// Reads each domain's handler-served query types from the admin operational index (DAPR state key
/// <c>admin:query-types:{domain}</c>, written by <c>AdminOperationalIndexHostedService</c> for every
/// domain whose metadata loaded successfully),
/// with a short in-memory cache mirroring <c>DaprCommandAggregateTypeResolver</c>. On any read failure it
/// reports "no handler", so routing fails safe to the projection-actor path.
/// </summary>
public sealed class DaprDomainQueryHandlerRegistry : IDomainQueryHandlerRegistry {
    private DaprClient daprClient { get; }
    private IOptions<CommandStatusOptions> commandStatusOptions { get; }
    private ILogger<DaprDomainQueryHandlerRegistry> logger { get; }
    private Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority? catalogAuthority { get; }

    /// <summary>Creates a catalog-enforced runtime entry point.</summary>
    public DaprDomainQueryHandlerRegistry(
        Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority authority,
        DaprClient daprClient,
        IOptions<CommandStatusOptions> commandStatusOptions,
        ILogger<DaprDomainQueryHandlerRegistry> logger)
        : this(daprClient, commandStatusOptions ?? throw new ArgumentNullException(nameof(commandStatusOptions)), logger, authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
    }

    /// <summary>Explicit friend-only composition seam for existing isolated protocol tests.</summary>
    internal DaprDomainQueryHandlerRegistry(
        DaprClient daprClient,
        IOptions<CommandStatusOptions> commandStatusOptions,
        ILogger<DaprDomainQueryHandlerRegistry> logger,
        Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority? catalogAuthority = null)
    {
        this.daprClient = daprClient;
        this.commandStatusOptions = commandStatusOptions;
        this.logger = logger;
        this.catalogAuthority = catalogAuthority;
        _stateStoreName = commandStatusOptions.Value.StateStoreName;
    }

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private readonly string _stateStoreName;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public async Task<bool> SupportsQueryAsync(string domain, string queryType, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryType);

        if (catalogAuthority is not null)
        {
            var lease = await catalogAuthority.AcquireAsync(cancellationToken).ConfigureAwait(false);
            await using var release = lease.ConfigureAwait(false);
            var declared = lease.Snapshot.Envelope.Declarations.Where(d => d.Kind == "query" && d.Domain == domain
                && Hexalith.EventStore.Contracts.Routing.CommandDiscriminatorCompatibility.Matches(d, queryType)).ToArray();
            if (declared.Length != 1) throw new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogUnavailableException("Query metadata cannot introduce an undeclared or ambiguous target.");
            // Operational metadata remains advisory. Cache it only under this exact committed fence;
            // absence, failures and stale positives never select another execution target.
            _ = await GetQueryTypesAsync(domain, cancellationToken, $"{lease.Snapshot.Epoch}:{lease.Snapshot.RootDigest}:{domain}").ConfigureAwait(false);
            if (declared[0].Execution is not { } execution)
                throw new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogUnavailableException("The committed declaration has no execution metadata.");
            return execution.TargetKind == "domain-service";
        }

        IReadOnlyCollection<string> queryTypes = await GetQueryTypesAsync(domain, cancellationToken).ConfigureAwait(false);
        return queryTypes.Contains(queryType, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyCollection<string>> GetQueryTypesAsync(string domain, CancellationToken cancellationToken, string? generationKey = null) {
        string cacheKey = generationKey ?? domain;
        string? generationPrefix = generationKey is null ? null : generationKey[..(generationKey.LastIndexOf(':') + 1)];
        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (var entry in _cache)
            if (entry.Value.ExpiresAtUtc <= now || (generationPrefix is not null && !entry.Key.StartsWith(generationPrefix, StringComparison.Ordinal)))
                _cache.TryRemove(entry.Key, out _);
        if (_cache.TryGetValue(cacheKey, out CacheEntry? cached) && cached.ExpiresAtUtc > DateTimeOffset.UtcNow) {
            return cached.QueryTypes;
        }

        IReadOnlyCollection<string> queryTypes;
        try {
            List<string>? stored = await daprClient
                .GetStateAsync<List<string>>(_stateStoreName, $"admin:query-types:{domain}", cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            queryTypes = stored is null ? [] : new HashSet<string>(stored, StringComparer.OrdinalIgnoreCase);
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

        _cache[cacheKey] = new CacheEntry(queryTypes, DateTimeOffset.UtcNow.Add(CacheTtl));
        return queryTypes;
    }

    private sealed record CacheEntry(IReadOnlyCollection<string> QueryTypes, DateTimeOffset ExpiresAtUtc);
}
