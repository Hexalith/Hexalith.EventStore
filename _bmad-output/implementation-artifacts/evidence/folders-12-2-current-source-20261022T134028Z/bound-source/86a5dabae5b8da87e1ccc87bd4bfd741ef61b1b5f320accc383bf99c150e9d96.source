using Hexalith.EventStore.Client.Processes;
using Hexalith.EventStore.Client.Projections;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

/// <summary>CAS authority for process checkpoints and crash-safe paged candidate publication.</summary>
public sealed class ProcessCheckpointStore(
    IReadModelStore store,
    IOptions<EventStoreProcessOptions> options,
    TimeProvider time)
{
    private readonly IReadModelStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly EventStoreProcessOptions _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
    private readonly TimeProvider _time = time ?? throw new ArgumentNullException(nameof(time));
    private readonly ProcessSweepStore _sweeps = new(store, options, time);

    /// <summary>Reads one durable source record, or an initial active state.</summary>
    public async Task<ProcessSourceState> GetAsync(ProcessSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Validate();
        ReadModelEntry<ProcessSourceState> entry = await ReadAsync<ProcessSourceState>(_options.StateStoreName, ProcessStateKeys.Source(source), cancellationToken).ConfigureAwait(false);
        ProcessSourceState state = entry.Value ?? Initial(source);
        state.Validate(source);
        return state;
    }

    /// <summary>Acquires exclusive source ownership, or returns null while another live owner holds it.</summary>
    public async Task<ProcessSourceState?> AcquireAsync(ProcessSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Validate();
        for (int attempt = 0; attempt < _options.WriteAttempts; attempt++)
        {
            ReadModelEntry<ProcessSourceState> entry = await ReadEntryAsync(source, cancellationToken).ConfigureAwait(false);
            ProcessSourceState current = entry.Value ?? Initial(source);
            current.Validate(source);
            DateTimeOffset now = _time.GetUtcNow();
            if (current.Owner is not null && current.LeaseUntil > now)
            {
                return null;
            }

            ProcessSourceState next = current with
            {
                Owner = Guid.NewGuid().ToString("N"),
                LeaseUntil = now + _options.LeaseDuration,
                Fence = checked(current.Fence + 1),
                UpdatedAt = now,
            };
            if (await SaveAsync(source, next, entry.ETag, cancellationToken).ConfigureAwait(false))
            {
                return next;
            }
        }

        throw new InvalidOperationException("Process source ownership CAS exhausted.");
    }

    /// <summary>Renews a live lease and verifies its fence before external work.</summary>
    public Task<ProcessSourceState> RenewAsync(ProcessSourceState owned, CancellationToken cancellationToken = default)
        => UpdateOwnedAsync(owned, state => state with { LeaseUntil = _time.GetUtcNow() + _options.LeaseDuration }, cancellationToken);

    /// <summary>Applies a checkpoint or unresolved change only under current source ownership.</summary>
    public async Task<ProcessSourceState> UpdateOwnedAsync(ProcessSourceState owned, Func<ProcessSourceState, ProcessSourceState> update, CancellationToken cancellationToken = default, ProcessSweepState? sweepOwner = null)
    {
        ArgumentNullException.ThrowIfNull(owned);
        ArgumentNullException.ThrowIfNull(update);
        for (int attempt = 0; attempt < _options.WriteAttempts; attempt++)
        {
            ReadModelEntry<ProcessSourceState> entry = await ReadEntryAsync(owned.Source, cancellationToken).ConfigureAwait(false);
            ProcessSourceState current = entry.Value ?? throw new InvalidOperationException("Owned source state disappeared.");
            current.Validate(owned.Source);
            if (current.Owner != owned.Owner || current.Fence != owned.Fence || current.LeaseUntil <= _time.GetUtcNow())
            {
                throw new InvalidOperationException("Process source ownership was lost.");
            }

            ProcessSourceState next = update(current) with { UpdatedAt = _time.GetUtcNow() };
            if (next.Status != current.Status || next.Checkpoint != current.Checkpoint
                || next.Attempts != current.Attempts || next.FailureSequence != current.FailureSequence
                || next.ReasonCode != current.ReasonCode)
            {
                ProcessPendingTransition transition = await _sweeps.InvalidateSourceAsync(owned.Source, sweepOwner, cancellationToken).ConfigureAwait(false);
                next = next with { LastTransitionGeneration = transition.Generation, LastTransitionToken = transition.Token };
            }
            if (next.Checkpoint < current.Checkpoint || next.Source != current.Source || next.Owner != current.Owner || next.Fence != current.Fence)
            {
                throw new InvalidOperationException("A process checkpoint may not regress or change ownership.");
            }

            next.Validate(owned.Source);
            if (await SaveAsync(owned.Source, next, entry.ETag, cancellationToken).ConfigureAwait(false))
            {
                return next;
            }
        }

        throw new InvalidOperationException("Process checkpoint CAS exhausted.");
    }

    /// <summary>Releases this owner's lease without changing another owner's state.</summary>
    public async Task ReleaseAsync(ProcessSourceState owned, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owned);
        for (int attempt = 0; attempt < _options.WriteAttempts; attempt++)
        {
            ReadModelEntry<ProcessSourceState> entry = await ReadEntryAsync(owned.Source, cancellationToken).ConfigureAwait(false);
            ProcessSourceState? current = entry.Value;
            if (current is null || current.Owner != owned.Owner || current.Fence != owned.Fence)
            {
                return;
            }

            if (await SaveAsync(owned.Source, current with { Owner = null, LeaseUntil = null, UpdatedAt = _time.GetUtcNow() }, entry.ETag, cancellationToken).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    /// <summary>Re-enters validation after a durable disposition preparation, once no live runner owns the source.</summary>
    public async Task<bool> RevalidateAsync(ProcessPendingDisposition claim, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ProcessSource source = claim.Source;
        for (int attempt = 0; attempt < _options.WriteAttempts; attempt++)
        {
            if (!await _sweeps.IsClaimCurrentAsync(claim, cancellationToken).ConfigureAwait(false)) return false;
            ReadModelEntry<ProcessSourceState> entry = await ReadEntryAsync(source, cancellationToken).ConfigureAwait(false);
            ProcessSourceState current = entry.Value ?? Initial(source);
            current.Validate(source);
            if (current.LastDispositionGeneration > claim.PreparedGeneration) return false;
            if (current.LastDispositionGeneration == claim.PreparedGeneration) return true;
            if (current.Owner is not null && current.LeaseUntil > _time.GetUtcNow()) return false;
            ProcessSourceState next = current with
            {
                Status = ProcessSourceStatus.Revalidating,
                Attempts = 0,
                ReasonCode = null,
                FailureSequence = null,
                Owner = null,
                LeaseUntil = null,
                Fence = checked(current.Fence + 1),
                LastDispositionGeneration = claim.PreparedGeneration,
                UpdatedAt = _time.GetUtcNow(),
            };
            if (await SaveAsync(source, next, entry.ETag, cancellationToken).ConfigureAwait(false)) return true;
        }

        throw new InvalidOperationException("Process disposition CAS exhausted.");
    }

    /// <summary>Publishes one candidate idempotently, including recovery after the marker write was lost.</summary>
    public async Task EnsureCandidateAsync(ProcessSource source, CancellationToken cancellationToken = default)
    {
        if (!await EnsureCandidateAsync(source, null, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Process candidate publication requires another repair pass.");
    }

    /// <summary>Publishes a candidate under an optional currently owned recovery pass.</summary>
    /// <returns>True when both markers are durable; false when bounded historical repair must continue.</returns>
    internal async Task<bool> EnsureCandidateAsync(ProcessSource source, ProcessSweepState? sweepOwner, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Validate();
        bool candidatePublished = await HasMarkerAsync(source, cancellationToken).ConfigureAwait(false);
        if (candidatePublished && await HasTenantMarkerAsync(source.Tenant, cancellationToken).ConfigureAwait(false))
        {
            await _sweeps.CompletePublicationAsync(source, cancellationToken).ConfigureAwait(false);
            return true;
        }
        bool fresh = await _sweeps.PreparePublicationAsync(source, sweepOwner, cancellationToken).ConfigureAwait(false);
        if (!candidatePublished)
        {
            // A published tenant proves that its candidate root once existed. A fresh
            // intent must not replace that missing root with this source alone.
            if (fresh && await HasTenantMarkerAsync(source.Tenant, cancellationToken).ConfigureAwait(false)
                && await ReadCandidatePageAsync(source.Tenant, 0, cancellationToken).ConfigureAwait(false) is null)
            {
                await _sweeps.MarkPublicationPageUnknownAsync(source, false, 1, cancellationToken).ConfigureAwait(false);
                return false;
            }
            bool? contained = fresh ? false : await ContainsInPagesAsync(source, cancellationToken).ConfigureAwait(false);
            if (contained is null) return false;
            if (!contained.Value && !await HasMarkerAsync(source, cancellationToken).ConfigureAwait(false)
                && !await AppendAsync(source.Tenant, source, source, false, cancellationToken).ConfigureAwait(false))
                return false;

            if (!await HasMarkerAsync(source, cancellationToken).ConfigureAwait(false))
                _ = await TrySaveAsync(_options.StateStoreName, ProcessStateKeys.CandidateMarker(source),
                    new ProcessCandidateMarker(1, source), string.Empty, cancellationToken).ConfigureAwait(false);
            if (!await HasMarkerAsync(source, cancellationToken).ConfigureAwait(false))
                throw new InvalidOperationException("Process candidate marker was not durable.");
        }
        // The candidate page is durable before its tenant becomes discoverable. An exact tenant
        // marker keeps later sources in this tenant from appending a duplicate registry entry.
        if (!await HasTenantMarkerAsync(source.Tenant, cancellationToken).ConfigureAwait(false))
        {
            // A crash after the tenant-page write but before its marker needs cross-page deduplication.
            bool mustSearchTenant = !fresh || await _sweeps.HasOtherPendingPublicationForTenantAsync(source, cancellationToken).ConfigureAwait(false);
            bool? contained = mustSearchTenant ? await ContainsTenantInPagesAsync(source, cancellationToken).ConfigureAwait(false) : false;
            if (contained is null) return false;
            if (!mustSearchTenant && !contained.Value
                && await ReadTenantPageAsync(0, cancellationToken).ConfigureAwait(false) is null)
            {
                ProcessSweepState sweep = await _sweeps.GetAsync(cancellationToken).ConfigureAwait(false);
                if (await ReadTenantTailAsync(cancellationToken).ConfigureAwait(false) is not null
                    || sweep.RetainedCandidates > sweep.PendingPublications.Count)
                {
                    await _sweeps.MarkPublicationPageUnknownAsync(source, true, 1, cancellationToken).ConfigureAwait(false);
                    return false;
                }
            }
            if (!contained.Value && !await HasTenantMarkerAsync(source.Tenant, cancellationToken).ConfigureAwait(false)
                && !await AppendAsync(null, source.Tenant, source, true, cancellationToken).ConfigureAwait(false))
                return false;
            if (!await HasTenantMarkerAsync(source.Tenant, cancellationToken).ConfigureAwait(false))
                _ = await TrySaveAsync(_options.StateStoreName, ProcessStateKeys.TenantMarker(source.Tenant),
                    new ProcessTenantMarker(1, source.Tenant), string.Empty, cancellationToken).ConfigureAwait(false);
            if (!await HasTenantMarkerAsync(source.Tenant, cancellationToken).ConfigureAwait(false))
                throw new InvalidOperationException("Process tenant marker was not durable.");
        }
        await _sweeps.CompletePublicationAsync(source, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Checks fully published membership using exact durable evidence only.</summary>
    public async Task<bool> ContainsCandidateAsync(ProcessSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!await HasMarkerAsync(source, cancellationToken).ConfigureAwait(false)) return false;
        if (!await HasTenantMarkerAsync(source.Tenant, cancellationToken).ConfigureAwait(false)) return false;
        // An unfinished publication may have written the marker before its tenant
        // became discoverable. Reconciliation repairs marker-write crashes separately.
        ProcessSweepState sweep = await _sweeps.GetAsync(cancellationToken).ConfigureAwait(false);
        return !sweep.PendingPublications.ContainsKey(ProcessStateKeys.SourceId(source));
    }

    /// <summary>Reads a tenant registry page. A null value means this page is absent.</summary>
    public Task<ProcessIndexPage<string>?> ReadTenantPageAsync(int page, CancellationToken cancellationToken = default)
        => ReadPageAsync<string>(null, page, cancellationToken);

    /// <summary>Reads a tenant candidate page. A null value means this page is absent.</summary>
    public Task<ProcessIndexPage<ProcessSource>?> ReadCandidatePageAsync(string tenant, int page, CancellationToken cancellationToken = default)
        => ReadPageAsync<ProcessSource>(tenant, page, cancellationToken);

    /// <summary>Reads the durable tenant tail used to distinguish an empty registry from missing pages.</summary>
    public Task<int?> ReadTenantTailAsync(CancellationToken cancellationToken = default)
        => ReadTailAsync(ProcessStateKeys.TenantsTail, cancellationToken);

    /// <summary>Reads the durable tail of one tenant's candidate pages.</summary>
    public Task<int?> ReadCandidateTailAsync(string tenant, CancellationToken cancellationToken = default)
        => ReadTailAsync(ProcessStateKeys.CandidatesTail(tenant), cancellationToken);

    /// <summary>Marks a source observed exactly once in a recovery cycle.</summary>
    public async Task<bool> MarkObservedAsync(ProcessSource source, long cycle, bool unresolved, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        for (int attempt = 0; attempt < _options.WriteAttempts; attempt++)
        {
            ReadModelEntry<ProcessSourceState> entry = await ReadEntryAsync(source, cancellationToken).ConfigureAwait(false);
            ProcessSourceState current = entry.Value ?? Initial(source);
            current.Validate(source);
            if (current.ObservedCycle > cycle || current.ObservedCycle == cycle && (current.ObservedUnresolved || !unresolved)) return false;
            if (await SaveAsync(source, current with
            {
                ObservedCycle = cycle,
                ObservedUnresolved = unresolved || current.ObservedCycle == cycle && current.ObservedUnresolved,
                UpdatedAt = _time.GetUtcNow(),
            }, entry.ETag, cancellationToken).ConfigureAwait(false)) return true;
        }

        throw new InvalidOperationException("Process observation CAS exhausted.");
    }

    private ProcessSourceState Initial(ProcessSource source) => new(1, source, 0, ProcessSourceStatus.Active, 0, null, null, null, null, 0, 0, _time.GetUtcNow());

    private Task<ReadModelEntry<ProcessSourceState>> ReadEntryAsync(ProcessSource source, CancellationToken cancellationToken)
        => ReadAsync<ProcessSourceState>(_options.StateStoreName, ProcessStateKeys.Source(source), cancellationToken);

    private Task<bool> SaveAsync(ProcessSource source, ProcessSourceState state, string? etag, CancellationToken cancellationToken)
        => TrySaveAsync(_options.StateStoreName, ProcessStateKeys.Source(source), state, etag ?? string.Empty, cancellationToken);

    private async Task<ReadModelEntry<T>> ReadAsync<T>(string storeName, string key, CancellationToken cancellationToken) where T : class
    {
        return await ProcessReadDeadline.RunControlAsync(ct => _store.GetAsync<T>(storeName, key, ct), _options.OperationTimeout, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> TrySaveAsync<T>(string storeName, string key, T value, string etag, CancellationToken cancellationToken) where T : class
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_options.OperationTimeout);
        return await _store.TrySaveAsync(storeName, key, value, etag, deadline.Token)
            .WaitAsync(_options.OperationTimeout, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> HasMarkerAsync(ProcessSource source, CancellationToken cancellationToken)
    {
        ProcessCandidateMarker? marker = (await ReadAsync<ProcessCandidateMarker>(_options.StateStoreName, ProcessStateKeys.CandidateMarker(source), cancellationToken).ConfigureAwait(false)).Value;
        if (marker is null) return false;
        if (marker.Version != 1 || marker.Source != source) throw new InvalidOperationException("Process candidate marker is invalid.");
        return true;
    }

    private async Task<int?> ReadTailAsync(string key, CancellationToken cancellationToken)
    {
        ProcessIndexTail? tail = (await ReadAsync<ProcessIndexTail>(_options.StateStoreName, key, cancellationToken).ConfigureAwait(false)).Value;
        if (tail is null) return null;
        if (tail.Version != 1 || tail.Page is < 0 or >= 1_000_000)
            throw new InvalidOperationException("Process discovery tail hint is invalid.");
        return tail.Page;
    }

    private async Task<bool> HasTenantMarkerAsync(string tenant, CancellationToken cancellationToken)
    {
        ProcessTenantMarker? marker = (await ReadAsync<ProcessTenantMarker>(_options.StateStoreName,
            ProcessStateKeys.TenantMarker(tenant), cancellationToken).ConfigureAwait(false)).Value;
        if (marker is null) return false;
        if (marker.Version != 1 || marker.Tenant != tenant) throw new InvalidOperationException("Process tenant marker is invalid.");
        return true;
    }

    private async Task<bool?> ContainsInPagesAsync(ProcessSource source, CancellationToken cancellationToken)
    {
        int start = await _sweeps.GetPublicationRepairPageAsync(source, false, cancellationToken).ConfigureAwait(false);
        for (int page = start, scanned = 0; scanned < 8; page++, scanned++)
        {
            ProcessIndexPage<ProcessSource>? current = await ReadCandidatePageAsync(source.Tenant, page, cancellationToken).ConfigureAwait(false);
            if (current is null)
            {
                if (page == 0)
                {
                    if (await HasTenantMarkerAsync(source.Tenant, cancellationToken).ConfigureAwait(false)
                        || await ReadCandidateTailAsync(source.Tenant, cancellationToken).ConfigureAwait(false) is not null)
                    {
                        await _sweeps.MarkPublicationPageUnknownAsync(source, false, 1, cancellationToken).ConfigureAwait(false);
                        return null;
                    }
                    return false;
                }
                // A linked page may have held other candidates. Its contents cannot be
                // reconstructed from this one publication intent.
                await _sweeps.MarkPublicationPageUnknownAsync(source, false, page + 1, cancellationToken).ConfigureAwait(false);
                return null;
            }
            if (current.Entries.Contains(source))
            {
                await AdvanceTailAsync(ProcessStateKeys.CandidatesTail(source.Tenant), page, cancellationToken).ConfigureAwait(false);
                return true;
            }
            if (current.NextPage is null)
            {
                await AdvanceTailAsync(ProcessStateKeys.CandidatesTail(source.Tenant), page, cancellationToken).ConfigureAwait(false);
                return await _sweeps.HasUnknownPublicationPageAsync(source, false, cancellationToken).ConfigureAwait(false) ? null : false;
            }
            if (scanned == 7)
            {
                await _sweeps.AdvancePublicationRepairPageAsync(source, false, current.NextPage.Value, cancellationToken).ConfigureAwait(false);
                return null;
            }
        }

        throw new InvalidOperationException("Process candidate repair cursor is invalid.");
    }

    private async Task<bool?> ContainsTenantInPagesAsync(ProcessSource source, CancellationToken cancellationToken)
    {
        int start = await _sweeps.GetPublicationRepairPageAsync(source, true, cancellationToken).ConfigureAwait(false);
        for (int page = start, scanned = 0; scanned < 8; page++, scanned++)
        {
            ProcessIndexPage<string>? current = await ReadTenantPageAsync(page, cancellationToken).ConfigureAwait(false);
            if (current is null)
            {
                if (page == 0)
                {
                    ProcessSweepState sweep = await _sweeps.GetAsync(cancellationToken).ConfigureAwait(false);
                    if (await ReadTenantTailAsync(cancellationToken).ConfigureAwait(false) is not null
                        || sweep.RetainedCandidates > sweep.PendingPublications.Count)
                    {
                        await _sweeps.MarkPublicationPageUnknownAsync(source, true, 1, cancellationToken).ConfigureAwait(false);
                        return null;
                    }
                    return false;
                }
                await _sweeps.MarkPublicationPageUnknownAsync(source, true, page + 1, cancellationToken).ConfigureAwait(false);
                return null;
            }
            if (current.Entries.Contains(source.Tenant, StringComparer.Ordinal))
            {
                await AdvanceTailAsync(ProcessStateKeys.TenantsTail, page, cancellationToken).ConfigureAwait(false);
                return true;
            }
            if (current.NextPage is null)
            {
                await AdvanceTailAsync(ProcessStateKeys.TenantsTail, page, cancellationToken).ConfigureAwait(false);
                return await _sweeps.HasUnknownPublicationPageAsync(source, true, cancellationToken).ConfigureAwait(false) ? null : false;
            }
            if (scanned == 7)
            {
                await _sweeps.AdvancePublicationRepairPageAsync(source, true, current.NextPage.Value, cancellationToken).ConfigureAwait(false);
                return null;
            }
        }

        throw new InvalidOperationException("Process tenant repair cursor is invalid.");
    }

    private async Task<bool> AppendAsync<T>(string? tenant, T item, ProcessSource source, bool tenantPage, CancellationToken cancellationToken)
    {
        string tailKey = tenant is null ? ProcessStateKeys.TenantsTail : ProcessStateKeys.CandidatesTail(tenant);
        ReadModelEntry<ProcessIndexTail> tailEntry = await ReadAsync<ProcessIndexTail>(_options.StateStoreName, tailKey, cancellationToken).ConfigureAwait(false);
        int repairPage = await _sweeps.GetPublicationRepairPageAsync(source, tenantPage, cancellationToken).ConfigureAwait(false);
        int firstPage = Math.Max(tailEntry.Value?.Page ?? 0, repairPage);
        if (tailEntry.Value is { } tail && (tail.Version != 1 || tail.Page is < 0 or >= 1_000_000))
            throw new InvalidOperationException("Process discovery tail hint is invalid.");
        int visited = 0;
        for (int restart = 0; restart < _options.WriteAttempts; restart++)
        {
            bool followedLink = false;
            for (int page = firstPage; page < 1_000_000; page++)
            {
                if (visited++ >= 8)
                {
                    if (page == 0) return false;
                    await _sweeps.AdvancePublicationRepairPageAsync(source, tenantPage, page, cancellationToken).ConfigureAwait(false);
                    return false;
                }
                string key = PageKey(tenant, page);
                ReadModelEntry<ProcessIndexPage<T>> entry = await ReadAsync<ProcessIndexPage<T>>(_options.StateStoreName, key, cancellationToken).ConfigureAwait(false);
                ProcessIndexPage<T>? current = entry.Value;
                if (current is null)
                {
                    if (followedLink)
                    {
                        await _sweeps.MarkPublicationPageUnknownAsync(source, tenantPage, page + 1, cancellationToken).ConfigureAwait(false);
                        return false;
                    }
                    if (page != 0 && firstPage == page)
                    {
                        // The tail or repair cursor names a page that is not there. Keep the
                        // hole unresolved and resume at the next page instead of inventing it.
                        await _sweeps.MarkPublicationPageUnknownAsync(source, tenantPage, page + 1, cancellationToken).ConfigureAwait(false);
                        return false;
                    }
                    _ = await TrySaveAsync(_options.StateStoreName, key, new ProcessIndexPage<T>(1, page, [item], null), string.Empty, cancellationToken).ConfigureAwait(false);
                    ProcessIndexPage<T>? written = await ReadPageAsync<T>(tenant, page, cancellationToken).ConfigureAwait(false);
                    if (written?.Entries.Contains(item) == true)
                    {
                        await AdvanceTailAsync(tailKey, page, cancellationToken).ConfigureAwait(false);
                        return true;
                    }
                    break;
                }

                ValidatePage(current, page, tenant);
                if (current.Entries.Contains(item))
                {
                    await AdvanceTailAsync(tailKey, page, cancellationToken).ConfigureAwait(false);
                    return true;
                }
                if (current.NextPage is { } nextPage)
                {
                    followedLink = true;
                    page = nextPage - 1;
                    continue;
                }

                if (current.Entries.Count < 256)
                {
                    ProcessIndexPage<T> expanded = current with { Entries = [.. current.Entries, item] };
                    if (await TrySaveAsync(_options.StateStoreName, key, expanded, entry.ETag ?? string.Empty, cancellationToken).ConfigureAwait(false))
                    {
                        await AdvanceTailAsync(tailKey, page, cancellationToken).ConfigureAwait(false);
                        return true;
                    }
                    break;
                }

                int following = checked(page + 1);
                string followingKey = PageKey(tenant, following);
                ProcessIndexPage<T>? existing = (await ReadAsync<ProcessIndexPage<T>>(_options.StateStoreName, followingKey, cancellationToken).ConfigureAwait(false)).Value;
                if (existing is null)
                {
                    _ = await TrySaveAsync(_options.StateStoreName, followingKey, new ProcessIndexPage<T>(1, following, [item], null), string.Empty, cancellationToken).ConfigureAwait(false);
                }

                // A rejected CAS can leave the page absent. Never publish a
                // parent link until the child is confirmed durable.
                if (await ReadPageAsync<T>(tenant, following, cancellationToken).ConfigureAwait(false) is null)
                    return false;

                if (await TrySaveAsync(_options.StateStoreName, key, current with { NextPage = following }, entry.ETag ?? string.Empty, cancellationToken).ConfigureAwait(false))
                {
                    break; // Revisit the newly linked page; it may have been written by a competitor.
                }

                break;
            }
        }

        throw new InvalidOperationException("Process candidate publication CAS exhausted.");
    }

    private async Task AdvanceTailAsync(string key, int page, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < _options.WriteAttempts; attempt++)
        {
            ReadModelEntry<ProcessIndexTail> entry = await ReadAsync<ProcessIndexTail>(_options.StateStoreName, key, cancellationToken).ConfigureAwait(false);
            if (entry.Value is { } current)
            {
                if (current.Version != 1 || current.Page is < 0 or >= 1_000_000) throw new InvalidOperationException("Process discovery tail hint is invalid.");
                if (current.Page >= page) return;
            }

            if (await TrySaveAsync(_options.StateStoreName, key, new ProcessIndexTail(1, page), entry.ETag ?? string.Empty, cancellationToken).ConfigureAwait(false)) return;
        }

        throw new InvalidOperationException("Process discovery tail update CAS exhausted.");
    }

    private async Task<ProcessIndexPage<T>?> ReadPageAsync<T>(string? tenant, int page, CancellationToken cancellationToken)
    {
        if (page < 0 || page >= 1_000_000) throw new ArgumentOutOfRangeException(nameof(page));
        ProcessIndexPage<T>? value = (await ReadAsync<ProcessIndexPage<T>>(_options.StateStoreName, PageKey(tenant, page), cancellationToken).ConfigureAwait(false)).Value;
        if (value is not null) ValidatePage(value, page, tenant);
        return value;
    }

    private static void ValidatePage<T>(ProcessIndexPage<T> page, int expected, string? tenant)
    {
        if (page.Version != 1 || page.Page != expected || page.Entries is null || page.Entries.Count is < 1 or > 256
            || page.NextPage is { } next && next != expected + 1
            || page.Entries.Any(static item => item is null)
            || page.Entries.Distinct().Count() != page.Entries.Count
            || tenant is null && page.Entries.OfType<string>().Any(static value => !IsCanonicalTenant(value))
            || tenant is not null && page.Entries.OfType<ProcessSource>().Any(source => source.Tenant != tenant || !IsValidSource(source)))
        {
            throw new InvalidOperationException("Process discovery page is invalid.");
        }
    }

    private static bool IsCanonicalTenant(string value)
        => value.Length is >= 1 and <= 64
            && (value[0] is >= 'a' and <= 'z' or >= '0' and <= '9')
            && (value[^1] is >= 'a' and <= 'z' or >= '0' and <= '9')
            && value.All(static character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');

    private static bool IsValidSource(ProcessSource source)
    {
        try
        {
            source.Validate();
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string PageKey(string? tenant, int page)
        => tenant is null ? ProcessStateKeys.Tenants(page) : ProcessStateKeys.Candidates(tenant, page);
}
