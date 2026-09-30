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

    /// <summary>Adds the tenant and the candidate when absent.</summary>
    /// <param name="target">The target stream.</param>
    /// <param name="actorId">The target's <c>wra-</c> actor identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when both documents hold the entries.</returns>
    /// <exception cref="ReminderFailClosedException">The CAS budget is exhausted or the tenant index is full.</exception>
    public async Task EnsureCandidateAsync(ReminderTarget target, string actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        await UpdateAsync<ReminderTenantRegistry>(
            ReminderStateKeys.TenantRegistry(_options.ActorTypeName),
            current =>
            {
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
            ReadModelEntry<TDocument> current = await _store
                .GetAsync<TDocument>(_options.StateStoreName, key, cancellationToken)
                .ConfigureAwait(false);
            TDocument? next = mutate(current.Value);
            if (next is null)
            {
                return;
            }

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
