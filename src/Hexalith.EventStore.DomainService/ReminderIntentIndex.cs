using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Reminders;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// The reminder discovery index: a control-namespace tenant registry plus one candidate document per tenant.
/// It is written by compare-and-swap before any reminder of an item is scheduled and its entry is removed
/// last, after the item's pending state is gone. Streams stay authoritative; the index only discovers
/// candidates for reconciliation.
/// </summary>
internal sealed class ReminderIntentIndex(IReadModelStore store, IOptions<EventStoreReminderOptions> options)
{
    private readonly IReadModelStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly EventStoreReminderOptions _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;

    /// <summary>Isolates bounded convergence admission by the owning SDK store instance.</summary>
    internal object AdmissionScope => _store;

    private string DiscoveryKey(bool indexed)
        => ReminderStateKeys.TenantRegistry(_options.ActorTypeName) + (indexed ? ":indexed" : ":catalog");

    /// <summary>Reads retained authoritative catalog discovery evidence.</summary>
    internal async Task<ReminderCatalogDiscoveryState?> ReadCatalogAsync(CancellationToken cancellationToken)
        => (await _store.GetAsync<ReminderCatalogDiscoveryState>(_options.StateStoreName,
            DiscoveryKey(false), cancellationToken).ConfigureAwait(false)).Value;

    /// <summary>Claims a persisted pending sweep before performing any provider work.</summary>
    internal async Task<ReminderCatalogDiscoveryState> ClaimDiscoveryAsync(bool indexed, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < _options.IndexWriteAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadModelEntry<ReminderCatalogDiscoveryState> current = await _store.GetAsync<ReminderCatalogDiscoveryState>(
                _options.StateStoreName, DiscoveryKey(indexed), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            string? after = current.Value?.Complete == false ? current.Value.AfterKey : null;
            bool incomplete = after is not null && current.Value?.IsIncomplete == true;
            var next = new ReminderCatalogDiscoveryState(false, after, incomplete ? "catalog-unavailable" : "catalog-incomplete")
            {
                IsIncomplete = incomplete,
                Generation = checked((current.Value?.Generation ?? 0) + 1),
                Owner = Guid.NewGuid().ToString("N"),
            };
            if (await _store.TrySaveAsync(_options.StateStoreName, DiscoveryKey(indexed), next,
                current.ETag ?? string.Empty, cancellationToken).ConfigureAwait(false)) return next;
        }
        throw new ReminderFailClosedException("index-conflict");
    }

    /// <summary>Commits only the exact current owned step. A failed proof CAS is never re-merged.</summary>
    internal async Task<ReminderCatalogDiscoveryState> UpdateDiscoveryAsync(bool indexed, ReminderCatalogDiscoveryState owned,
        bool complete, string? after, bool incomplete, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string key = DiscoveryKey(indexed);
        ReadModelEntry<ReminderCatalogDiscoveryState> current = await _store.GetAsync<ReminderCatalogDiscoveryState>(
            _options.StateStoreName, key, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (current.Value?.Owner != owned.Owner || current.Value?.Generation != owned.Generation
            || current.Value?.Revision != owned.Revision)
            throw new ReminderFailClosedException("index-conflict");
        var next = owned with
        {
            Complete = complete,
            AfterKey = after,
            IsIncomplete = incomplete,
            ReasonCode = complete ? null : incomplete ? "catalog-unavailable" : "catalog-incomplete",
            Revision = checked(owned.Revision + 1),
        };
        if (!await _store.TrySaveAsync(_options.StateStoreName, key, next, current.ETag ?? string.Empty,
            cancellationToken).ConfigureAwait(false)) throw new ReminderFailClosedException("index-conflict");
        cancellationToken.ThrowIfCancellationRequested();
        return next;
    }

    /// <summary>Records external failure evidence with one conditional transition, never retrying a stale proof.</summary>
    internal async Task RecordCatalogAsync(bool complete, string? afterKey, string? reason, CancellationToken cancellationToken, bool isIncomplete = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string key = DiscoveryKey(false);
        ReadModelEntry<ReminderCatalogDiscoveryState> current = await _store.GetAsync<ReminderCatalogDiscoveryState>(
            _options.StateStoreName, key, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var next = new ReminderCatalogDiscoveryState(complete, afterKey, reason)
        {
            IsIncomplete = isIncomplete || !complete && afterKey is not null && current.Value?.IsIncomplete == true,
            Generation = checked((current.Value?.Generation ?? 0) + 1),
            Owner = Guid.NewGuid().ToString("N"),
        };
        if (!await _store.TrySaveAsync(_options.StateStoreName, key, next, current.ETag ?? string.Empty,
            cancellationToken).ConfigureAwait(false)) throw new ReminderFailClosedException("index-conflict");
    }

    /// <summary>Adds the tenant and the candidate when absent.</summary>
    /// <param name="target">The target stream.</param>
    /// <param name="actorId">The target's <c>wra-</c> actor identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when both documents hold the entries.</returns>
    /// <exception cref="ReminderFailClosedException">A discovery collection is malformed, the CAS budget is exhausted, or the tenant index is full.</exception>
    public async Task EnsureCandidateAsync(ReminderTarget target, string actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        await UpdateAsync<ReminderTenantRegistry>(
            ReminderStateKeys.TenantRegistry(_options.ActorTypeName),
            current =>
            {
                if (current is not null && current.Tenants is null)
                {
                    throw new ReminderFailClosedException("index-registry-invalid");
                }

                IReadOnlyList<string> tenants = current?.Tenants ?? [];
                return tenants.Contains(target.Tenant, StringComparer.Ordinal)
                    ? null
                    : new ReminderTenantRegistry([.. tenants.Append(target.Tenant).Order(StringComparer.Ordinal)]);
            },
            cancellationToken).ConfigureAwait(false);

        var candidate = new ReminderCandidate(target.Domain, target.Aggregate, actorId);
        await UpdateAsync<ReminderTenantCandidates>(
            ReminderStateKeys.TenantCandidates(_options.ActorTypeName, target.Tenant),
            current =>
            {
                if (current is not null && current.Candidates is null)
                {
                    throw new ReminderFailClosedException("index-candidates-invalid");
                }

                IReadOnlyList<ReminderCandidate> candidates = current?.Candidates ?? [];
                if (candidates.Contains(candidate))
                {
                    return null;
                }

                if (candidates.Count >= _options.MaxCandidatesPerTenant)
                {
                    throw new ReminderFailClosedException("index-capacity");
                }

                return new ReminderTenantCandidates(
                    target.Tenant,
                    [.. candidates.Append(candidate).OrderBy(static c => c.ActorId, StringComparer.Ordinal)
                        .ThenBy(static c => c.Domain, StringComparer.Ordinal)]);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Removes the candidate when present. The tenant stays registered.</summary>
    /// <param name="target">The target stream.</param>
    /// <param name="actorId">The target's <c>wra-</c> actor identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the candidate is absent.</returns>
    public Task RemoveCandidateAsync(ReminderTarget target, string actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        var candidate = new ReminderCandidate(target.Domain, target.Aggregate, actorId);
        return UpdateAsync<ReminderTenantCandidates>(
            ReminderStateKeys.TenantCandidates(_options.ActorTypeName, target.Tenant),
            current =>
            {
                // A null list is corrupt discovery data, not a list that contains this candidate.
                if (current?.Candidates is not IReadOnlyList<ReminderCandidate> candidates
                    || !candidates.Contains(candidate))
                {
                    return null;
                }

                return current with { Candidates = [.. candidates.Where(c => c != candidate)] };
            },
            cancellationToken);
    }

    /// <summary>Lists the registered tenants.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The tenants in ordinal order.</returns>
    public async Task<IReadOnlyList<string>> ListTenantsAsync(CancellationToken cancellationToken)
    {
        ReadModelEntry<ReminderTenantRegistry> entry = await _store
            .GetAsync<ReminderTenantRegistry>(
                _options.StateStoreName,
                ReminderStateKeys.TenantRegistry(_options.ActorTypeName),
                cancellationToken)
            .ConfigureAwait(false);
        if (entry.Value is not null && entry.Value.Tenants is null)
        {
            throw new ReminderFailClosedException("index-registry-invalid");
        }

        return entry.Value?.Tenants ?? [];
    }

    /// <summary>Lists one tenant's candidates.</summary>
    /// <param name="tenant">The canonical tenant.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The candidates in actor-identifier order.</returns>
    public async Task<IReadOnlyList<ReminderCandidate>> ListCandidatesAsync(string tenant, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ReadModelEntry<ReminderTenantCandidates> entry = await _store
            .GetAsync<ReminderTenantCandidates>(
                _options.StateStoreName,
                ReminderStateKeys.TenantCandidates(_options.ActorTypeName, tenant),
                cancellationToken)
            .ConfigureAwait(false);
        if (entry.Value is not null && !string.Equals(entry.Value.Tenant, tenant, StringComparison.Ordinal))
        {
            // A document under one tenant's key that names another tenant is corrupt; never cross tenants.
            throw new ReminderFailClosedException("index-tenant-mismatch");
        }

        if (entry.Value is not null && entry.Value.Candidates is null)
        {
            throw new ReminderFailClosedException("index-candidates-invalid");
        }

        return entry.Value?.Candidates ?? [];
    }

    private async Task UpdateAsync<TDocument>(
        string key,
        Func<TDocument?, TDocument?> mutate,
        CancellationToken cancellationToken)
        where TDocument : class
    {
        for (int attempt = 0; attempt < _options.IndexWriteAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadModelEntry<TDocument> current = await _store
                .GetAsync<TDocument>(_options.StateStoreName, key, cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            TDocument? next = mutate(current.Value);
            if (next is null)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (await _store
                .TrySaveAsync(_options.StateStoreName, key, next, current.ETag ?? string.Empty, cancellationToken)
                .ConfigureAwait(false))
            {
                return;
            }
        }

        throw new ReminderFailClosedException("index-conflict");
    }
}
