using System.Text.Json;

using Hexalith.EventStore.Client.Processes;
using Hexalith.EventStore.Client.Projections;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

/// <summary>CAS authority for fair recovery continuation and disposition invalidation.</summary>
public sealed class ProcessSweepStore(IReadModelStore store, IOptions<EventStoreProcessOptions> options, TimeProvider time)
{
    private readonly IReadModelStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly EventStoreProcessOptions _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
    private readonly TimeProvider _time = time ?? throw new ArgumentNullException(nameof(time));

    /// <summary>Reads and validates the shared sweep, or returns an unstarted state.</summary>
    public async Task<ProcessSweepState> GetAsync(CancellationToken cancellationToken = default)
    {
        ProcessSweepState state = (await ReadAsync(cancellationToken).ConfigureAwait(false)).Value ?? Initial();
        state.Validate(_time);
        return state;
    }

    /// <summary>Whether another unfinished publication may already have linked this tenant.</summary>
    public async Task<bool> HasOtherPendingPublicationForTenantAsync(ProcessSource source, CancellationToken cancellationToken = default)
    {
        ProcessSweepState state = await GetAsync(cancellationToken).ConfigureAwait(false);
        return state.PendingPublications.Values.Any(candidate => candidate.Tenant == source.Tenant && candidate != source);
    }

    /// <summary>Reads the durable next page for one pending publication repair.</summary>
    public async Task<int> GetPublicationRepairPageAsync(ProcessSource source, bool tenantPage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ProcessSweepState state = await GetAsync(cancellationToken).ConfigureAwait(false);
        string key = ProcessStateKeys.SourceId(source);
        IReadOnlyDictionary<string, int> pages = tenantPage ? state.PublicationTenantRepairPages : state.PublicationCandidateRepairPages;
        return pages.TryGetValue(key, out int page) ? page : 0;
    }

    /// <summary>Advances a pending publication's historical scan without losing its exact source identity.</summary>
    public async Task AdvancePublicationRepairPageAsync(ProcessSource source, bool tenantPage, int nextPage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (nextPage is < 1 or >= 1_000_000) throw new ArgumentOutOfRangeException(nameof(nextPage));
        string key = ProcessStateKeys.SourceId(source);
        for (int attempt = 0; attempt < _options.WriteAttempts; attempt++)
        {
            ReadModelEntry<ProcessSweepState> entry = await ReadAsync(cancellationToken).ConfigureAwait(false);
            ProcessSweepState current = entry.Value ?? Initial();
            current.Validate(_time);
            if (!current.PendingPublications.TryGetValue(key, out ProcessSource? pending)) return;
            if (pending != source) throw new InvalidOperationException("Process publication identity conflict.");
            IReadOnlyDictionary<string, int> pages = tenantPage ? current.PublicationTenantRepairPages : current.PublicationCandidateRepairPages;
            if (pages.TryGetValue(key, out int page) && page >= nextPage) return;
            var updated = new Dictionary<string, int>(pages, StringComparer.Ordinal) { [key] = nextPage };
            ProcessSweepState next = tenantPage
                ? current with { PublicationTenantRepairPages = updated }
                : current with { PublicationCandidateRepairPages = updated };
            if (await SaveAsync(next, entry.ETag, cancellationToken).ConfigureAwait(false)) return;
        }

        throw new InvalidOperationException("Process publication repair cursor CAS exhausted.");
    }

    /// <summary>Retains evidence that a linked page cannot be reconstructed from one source identity.</summary>
    public async Task MarkPublicationPageUnknownAsync(ProcessSource source, bool tenantPage, int nextPage, CancellationToken cancellationToken = default)
    {
        if (nextPage is < 1 or >= 1_000_000) throw new ArgumentOutOfRangeException(nameof(nextPage));
        string key = ProcessStateKeys.SourceId(source);
        for (int attempt = 0; attempt < _options.WriteAttempts; attempt++)
        {
            ReadModelEntry<ProcessSweepState> entry = await ReadAsync(cancellationToken).ConfigureAwait(false);
            ProcessSweepState current = entry.Value ?? Initial();
            current.Validate(_time);
            if (!current.PendingPublications.ContainsKey(key)) return;
            IReadOnlyDictionary<string, bool> unknown = tenantPage ? current.PublicationTenantUnknownPages : current.PublicationCandidateUnknownPages;
            var updated = new Dictionary<string, bool>(unknown, StringComparer.Ordinal) { [key] = true };
            ProcessSweepState next;
            if (tenantPage)
            {
                var pages = new Dictionary<string, int>(current.PublicationTenantRepairPages, StringComparer.Ordinal)
                { [key] = Math.Max(nextPage, current.PublicationTenantRepairPages.GetValueOrDefault(key)) };
                next = current with { PublicationTenantUnknownPages = updated, PublicationTenantRepairPages = pages };
            }
            else
            {
                var pages = new Dictionary<string, int>(current.PublicationCandidateRepairPages, StringComparer.Ordinal)
                { [key] = Math.Max(nextPage, current.PublicationCandidateRepairPages.GetValueOrDefault(key)) };
                next = current with { PublicationCandidateUnknownPages = updated, PublicationCandidateRepairPages = pages };
            }
            if (await SaveAsync(next, entry.ETag, cancellationToken).ConfigureAwait(false)) return;
        }

        throw new InvalidOperationException("Process unknown-page marker CAS exhausted.");
    }

    /// <summary>Whether any earlier linked page in this publication had unknown contents.</summary>
    public async Task<bool> HasUnknownPublicationPageAsync(ProcessSource source, bool tenantPage, CancellationToken cancellationToken = default)
    {
        ProcessSweepState state = await GetAsync(cancellationToken).ConfigureAwait(false);
        string key = ProcessStateKeys.SourceId(source);
        return (tenantPage ? state.PublicationTenantUnknownPages : state.PublicationCandidateUnknownPages).ContainsKey(key);
    }

    /// <summary>Acquires one bounded pass, starting a fresh cycle after a completed cycle.</summary>
    public async Task<ProcessSweepState?> AcquireAsync(CancellationToken cancellationToken = default)
    {
        for (int attempt = 0; attempt < _options.WriteAttempts; attempt++)
        {
            ReadModelEntry<ProcessSweepState> entry = await ReadAsync(cancellationToken).ConfigureAwait(false);
            ProcessSweepState current = entry.Value ?? Initial();
            current.Validate(_time);
            DateTimeOffset now = _time.GetUtcNow();
            // A lease beyond the validated maximum plus one minute was not written by a
            // healthy owner. Take the sweep so that timestamp cannot freeze every replica.
            DateTimeOffset legalLeaseEnd = now + EventStoreProcessOptions.MaximumLeaseDuration + TimeSpan.FromMinutes(1);
            if (current.Owner is not null && current.LeaseUntil is { } leaseUntil && leaseUntil > now && leaseUntil <= legalLeaseEnd)
                return null;
            bool rescanPending = current.CatalogComplete && current.CandidatesComplete
                && current.PendingObservations.Count == 0
                && (current.PendingTransitions.Count != 0 || current.Pending.Values.Any(static pending => pending.Applied));
            ProcessSweepState next = current.Complete || current.Cycle == 0 || rescanPending
                ? ResetCycle(current, now)
                : current;
            next = next with
            {
                Owner = Guid.NewGuid().ToString("N"),
                LeaseUntil = now + _options.LeaseDuration,
                Fence = checked(current.Fence + 1),
                CapacityExceeded = current.RetainedCandidates > _options.SupportedCandidateCount,
            };
            if (await SaveAsync(next, entry.ETag, cancellationToken).ConfigureAwait(false)) return next;
        }

        throw new InvalidOperationException("Process sweep acquisition CAS exhausted.");
    }

    /// <summary>Publishes an exact source identity before any candidate index write.</summary>
    /// <returns>True only for a new intent; a retry must search for an earlier page append.</returns>
    public async Task<bool> PreparePublicationAsync(ProcessSource source, ProcessSweepState? sweepOwner = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Validate();
        string key = ProcessStateKeys.SourceId(source);
        for (int attempt = 0; attempt < _options.WriteAttempts; attempt++)
        {
            ReadModelEntry<ProcessSweepState> entry = await ReadAsync(cancellationToken).ConfigureAwait(false);
            ProcessSweepState current = entry.Value ?? Initial();
            current.Validate(_time);
            if (current.PendingPublications.TryGetValue(key, out ProcessSource? existing))
            {
                if (existing != source) throw new InvalidOperationException("Process publication identity conflict.");
                return false;
            }
            if (current.PendingPublications.Count >= 128)
                throw new InvalidOperationException("Process pending publication budget exhausted.");

            bool inOwnedPass = sweepOwner is not null && current.Owner == sweepOwner.Owner
                && current.Fence == sweepOwner.Fence && current.Generation == sweepOwner.Generation
                && current.LeaseUntil > _time.GetUtcNow() && !current.Complete;
            ProcessSweepState next = inOwnedPass ? current : ResetCycle(current, _time.GetUtcNow());
            next = next with
            {
                RetainedCandidates = checked(current.RetainedCandidates + 1),
                CapacityExceeded = current.RetainedCandidates + 1 > _options.SupportedCandidateCount,
                PendingPublications = new Dictionary<string, ProcessSource>(current.PendingPublications, StringComparer.Ordinal)
                {
                    [key] = source,
                },
            };
            if (JsonSerializer.SerializeToUtf8Bytes(next.PendingPublications).Length > 32 * 1024)
                throw new InvalidOperationException("Process pending publication byte budget exhausted.");
            if (await SaveAsync(next, entry.ETag, cancellationToken).ConfigureAwait(false)) return true;
        }

        throw new InvalidOperationException("Process publication preparation CAS exhausted.");
    }

    /// <summary>Removes an intent only after its candidate, marker, tenant index, and tenant marker are durable.</summary>
    public async Task CompletePublicationAsync(ProcessSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        string key = ProcessStateKeys.SourceId(source);
        for (int attempt = 0; attempt < _options.WriteAttempts; attempt++)
        {
            ReadModelEntry<ProcessSweepState> entry = await ReadAsync(cancellationToken).ConfigureAwait(false);
            ProcessSweepState current = entry.Value ?? Initial();
            current.Validate(_time);
            if (!current.PendingPublications.TryGetValue(key, out ProcessSource? existing)) return;
            if (existing != source) throw new InvalidOperationException("Process publication identity conflict.");
            var remaining = new Dictionary<string, ProcessSource>(current.PendingPublications, StringComparer.Ordinal);
            _ = remaining.Remove(key);
            var candidatePages = new Dictionary<string, int>(current.PublicationCandidateRepairPages, StringComparer.Ordinal);
            var tenantPages = new Dictionary<string, int>(current.PublicationTenantRepairPages, StringComparer.Ordinal);
            _ = candidatePages.Remove(key);
            _ = tenantPages.Remove(key);
            var candidateUnknown = new Dictionary<string, bool>(current.PublicationCandidateUnknownPages, StringComparer.Ordinal);
            var tenantUnknown = new Dictionary<string, bool>(current.PublicationTenantUnknownPages, StringComparer.Ordinal);
            _ = candidateUnknown.Remove(key);
            _ = tenantUnknown.Remove(key);
            if (await SaveAsync(current with
            {
                PendingPublications = remaining,
                PublicationCandidateRepairPages = candidatePages,
                PublicationTenantRepairPages = tenantPages,
                PublicationCandidateUnknownPages = candidateUnknown,
                PublicationTenantUnknownPages = tenantUnknown,
            }, entry.ETag, cancellationToken).ConfigureAwait(false)) return;
        }

        throw new InvalidOperationException("Process publication completion CAS exhausted.");
    }

    /// <summary>Whether a failed publication has a durable recovery identity.</summary>
    public async Task<bool> HasPendingPublicationAsync(ProcessSource source, CancellationToken cancellationToken = default)
        => (await GetAsync(cancellationToken).ConfigureAwait(false)).PendingPublications.ContainsKey(ProcessStateKeys.SourceId(source));

    /// <summary>Persists the last repair selection so a failed item cannot monopolize repair turns.</summary>
    public async Task AdvanceRepairAsync(string key, bool publication, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        for (int attempt = 0; attempt < _options.WriteAttempts; attempt++)
        {
            ReadModelEntry<ProcessSweepState> entry = await ReadAsync(cancellationToken).ConfigureAwait(false);
            ProcessSweepState current = entry.Value ?? Initial();
            current.Validate(_time);
            ProcessSweepState next = publication
                ? current with { PublicationRepairAfterKey = key, NextPublicationRepair = false }
                : current with { DispositionRepairAfterKey = key, NextPublicationRepair = true };
            if (await SaveAsync(next, entry.ETag, cancellationToken).ConfigureAwait(false)) return;
        }

        throw new InvalidOperationException("Process repair cursor CAS exhausted.");
    }

    /// <summary>Invalidates completed coverage before a direct source transition becomes visible.</summary>
    public async Task<ProcessPendingTransition> InvalidateSourceAsync(ProcessSource source, ProcessSweepState? sweepOwner = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Validate();
        for (int attempt = 0; attempt < _options.WriteAttempts; attempt++)
        {
            ReadModelEntry<ProcessSweepState> entry = await ReadAsync(cancellationToken).ConfigureAwait(false);
            ProcessSweepState current = entry.Value ?? Initial();
            current.Validate(_time);
            string key = ProcessStateKeys.SourceId(source);
            if (!current.PendingTransitions.ContainsKey(key) && current.PendingTransitions.Count >= 1024)
                throw new InvalidOperationException("Process transition handoff budget exhausted.");
            bool inOwnedPass = sweepOwner is not null && current.Owner == sweepOwner.Owner && current.Fence == sweepOwner.Fence
                && current.Generation == sweepOwner.Generation && current.LeaseUntil > _time.GetUtcNow() && !current.Complete;
            ProcessSweepState next = inOwnedPass ? current : ResetCycle(current, _time.GetUtcNow());
            var transition = new ProcessPendingTransition(source, next.Generation, Guid.NewGuid().ToString("N"));
            next = next with
            {
                PendingTransitions = new Dictionary<string, ProcessPendingTransition>(next.PendingTransitions, StringComparer.Ordinal)
                {
                    [key] = transition,
                },
            };
            if (await SaveAsync(next, entry.ETag, cancellationToken).ConfigureAwait(false)) return transition;
        }

        throw new InvalidOperationException("Process coverage invalidation CAS exhausted.");
    }

    /// <summary>Updates a pass only while its ownership and disposition generation remain current.</summary>
    public async Task<ProcessSweepState> UpdateOwnedAsync(ProcessSweepState owned, Func<ProcessSweepState, ProcessSweepState> update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owned);
        ArgumentNullException.ThrowIfNull(update);
        for (int attempt = 0; attempt < _options.WriteAttempts; attempt++)
        {
            ReadModelEntry<ProcessSweepState> entry = await ReadAsync(cancellationToken).ConfigureAwait(false);
            ProcessSweepState current = entry.Value ?? throw new InvalidOperationException("Owned process sweep disappeared.");
            current.Validate(_time);
            if (current.Owner != owned.Owner || current.Fence != owned.Fence || current.Generation != owned.Generation || current.LeaseUntil <= _time.GetUtcNow())
                throw new InvalidOperationException("Process sweep ownership or generation was lost.");
            ProcessSweepState next = update(current) with { LeaseUntil = _time.GetUtcNow() + _options.LeaseDuration };
            next.Validate(_time);
            if (await SaveAsync(next, entry.ETag, cancellationToken).ConfigureAwait(false)) return next;
        }

        throw new InvalidOperationException("Process sweep update CAS exhausted.");
    }

    /// <summary>Durably invalidates every readiness reader before source state mutation.</summary>
    public async Task<long> PrepareDispositionAsync(ProcessSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Validate();
        string key = ProcessStateKeys.SourceId(source);
        for (int attempt = 0; attempt < _options.WriteAttempts; attempt++)
        {
            ReadModelEntry<ProcessSweepState> entry = await ReadAsync(cancellationToken).ConfigureAwait(false);
            ProcessSweepState current = entry.Value ?? Initial();
            current.Validate(_time);
            if (current.Pending.TryGetValue(key, out ProcessPendingDisposition? pending))
            {
                if (pending.Source != source) throw new InvalidOperationException("Process pending identity conflict.");
            }
            else if (current.Pending.Count >= 1024) throw new InvalidOperationException("Process pending disposition budget exhausted.");
            ProcessSweepState next = ResetCycle(current, _time.GetUtcNow());
            next = next with
            {
                Pending = new Dictionary<string, ProcessPendingDisposition>(current.Pending, StringComparer.Ordinal)
                {
                    [key] = new(source, false, next.Generation, 0),
                },
            };
            if (await SaveAsync(next, entry.ETag, cancellationToken).ConfigureAwait(false)) return next.Generation;
        }

        throw new InvalidOperationException("Process disposition preparation CAS exhausted.");
    }

    /// <summary>Claims a current unapplied generation before touching its source record.</summary>
    public async Task<ProcessPendingDisposition?> ClaimDispositionAsync(ProcessSource source, long preparedGeneration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        string key = ProcessStateKeys.SourceId(source);
        for (int attempt = 0; attempt < _options.WriteAttempts; attempt++)
        {
            ReadModelEntry<ProcessSweepState> entry = await ReadAsync(cancellationToken).ConfigureAwait(false);
            ProcessSweepState current = entry.Value ?? Initial();
            current.Validate(_time);
            if (!current.Pending.TryGetValue(key, out ProcessPendingDisposition? pending)
                || pending.Source != source || pending.PreparedGeneration != preparedGeneration || pending.Applied)
                return null;
            if (pending.ClaimToken is not null && pending.ClaimUntil > _time.GetUtcNow()) return null;
            ProcessPendingDisposition claimed = pending with
            {
                ClaimToken = Guid.NewGuid().ToString("N"),
                ClaimUntil = _time.GetUtcNow() + _options.LeaseDuration,
            };
            ProcessSweepState next = current with
            {
                Pending = new Dictionary<string, ProcessPendingDisposition>(current.Pending, StringComparer.Ordinal)
                {
                    [key] = claimed,
                },
            };
            if (await SaveAsync(next, entry.ETag, cancellationToken).ConfigureAwait(false)) return claimed;
        }

        throw new InvalidOperationException("Process disposition claim CAS exhausted.");
    }

    /// <summary>Checks that an unapplied claim still owns its exact generation.</summary>
    public async Task<bool> IsClaimCurrentAsync(ProcessPendingDisposition claim, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ProcessSweepState current = await GetAsync(cancellationToken).ConfigureAwait(false);
        return current.Pending.TryGetValue(ProcessStateKeys.SourceId(claim.Source), out ProcessPendingDisposition? pending)
            && pending.Source == claim.Source && pending.PreparedGeneration == claim.PreparedGeneration
            && !pending.Applied && pending.ClaimToken is not null && pending.ClaimToken == claim.ClaimToken
            && pending.ClaimUntil > _time.GetUtcNow();
    }

    /// <summary>Releases an exact claim when a live source owner prevents mutation.</summary>
    public async Task ReleaseDispositionClaimAsync(ProcessPendingDisposition claim, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        string key = ProcessStateKeys.SourceId(claim.Source);
        for (int attempt = 0; attempt < _options.WriteAttempts; attempt++)
        {
            ReadModelEntry<ProcessSweepState> entry = await ReadAsync(cancellationToken).ConfigureAwait(false);
            ProcessSweepState current = entry.Value ?? Initial();
            current.Validate(_time);
            if (!current.Pending.TryGetValue(key, out ProcessPendingDisposition? pending)
                || pending.Source != claim.Source || pending.PreparedGeneration != claim.PreparedGeneration
                || pending.Applied || pending.ClaimToken != claim.ClaimToken)
                return;
            ProcessSweepState next = current with
            {
                Pending = new Dictionary<string, ProcessPendingDisposition>(current.Pending, StringComparer.Ordinal)
                {
                    [key] = pending with { ClaimToken = null, ClaimUntil = null },
                },
            };
            if (await SaveAsync(next, entry.ETag, cancellationToken).ConfigureAwait(false)) return;
        }

        throw new InvalidOperationException("Process disposition claim release CAS exhausted.");
    }

    /// <summary>Marks source mutation complete and resets coverage so its later observation is mandatory.</summary>
    public async Task<bool> MarkDispositionAppliedAsync(ProcessPendingDisposition claim, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ProcessSource source = claim.Source;
        string key = ProcessStateKeys.SourceId(source);
        for (int attempt = 0; attempt < _options.WriteAttempts; attempt++)
        {
            ReadModelEntry<ProcessSweepState> entry = await ReadAsync(cancellationToken).ConfigureAwait(false);
            ProcessSweepState current = entry.Value ?? throw new InvalidOperationException("Prepared process disposition disappeared.");
            current.Validate(_time);
            if (!current.Pending.TryGetValue(key, out ProcessPendingDisposition? pending) || pending.Source != source)
                throw new InvalidOperationException("Prepared process disposition disappeared.");
            if (pending.PreparedGeneration != claim.PreparedGeneration || pending.ClaimToken != claim.ClaimToken
                || pending.ClaimUntil <= _time.GetUtcNow()) return false;
            if (pending.Applied) return true;
            ProcessSweepState next = ResetCycle(current, _time.GetUtcNow());
            next = next with
            {
                Pending = new Dictionary<string, ProcessPendingDisposition>(current.Pending, StringComparer.Ordinal)
                {
                    [key] = pending with { Applied = true, AppliedGeneration = next.Generation, ClaimToken = null, ClaimUntil = null },
                },
            };
            if (await SaveAsync(next, entry.ETag, cancellationToken).ConfigureAwait(false)) return true;
        }

        throw new InvalidOperationException("Process disposition application CAS exhausted.");
    }

    /// <summary>Clears pending disposition only after its source was scanned in the applied generation.</summary>
    public Task<ProcessSweepState> CoverAsync(ProcessSweepState owned, ProcessSource source, CancellationToken cancellationToken = default)
        => UpdateOwnedAsync(owned, current =>
        {
            string key = ProcessStateKeys.SourceId(source);
            if (!current.Pending.TryGetValue(key, out ProcessPendingDisposition? pending)
                || !pending.Applied || pending.AppliedGeneration > current.Generation) return current;
            var remaining = new Dictionary<string, ProcessPendingDisposition>(current.Pending, StringComparer.Ordinal);
            _ = remaining.Remove(key);
            return current with { Pending = remaining };
        }, cancellationToken);

    /// <summary>Clears a counted observation and pending disposition only after its source marker is durable.</summary>
    public Task<ProcessSweepState> FinishObservationAsync(ProcessSweepState owned, ProcessSource source, string? observedTransitionToken, CancellationToken cancellationToken = default)
        => UpdateOwnedAsync(owned, current =>
        {
            string key = ProcessStateKeys.SourceId(source);
            if (!current.PendingObservations.ContainsKey(key))
                throw new InvalidOperationException("No counted process observation is pending.");
            var observations = new Dictionary<string, ProcessPendingObservation>(current.PendingObservations, StringComparer.Ordinal);
            _ = observations.Remove(key);
            IReadOnlyDictionary<string, ProcessPendingDisposition> pending = current.Pending;
            if (pending.TryGetValue(key, out ProcessPendingDisposition? disposition)
                && disposition.Applied && disposition.AppliedGeneration <= current.Generation)
            {
                var remaining = new Dictionary<string, ProcessPendingDisposition>(pending, StringComparer.Ordinal);
                _ = remaining.Remove(key);
                pending = remaining;
            }

            IReadOnlyDictionary<string, ProcessPendingTransition> transitions = current.PendingTransitions;
            if (transitions.TryGetValue(key, out ProcessPendingTransition? transition)
                && observedTransitionToken == transition.Token)
            {
                var remaining = new Dictionary<string, ProcessPendingTransition>(transitions, StringComparer.Ordinal);
                _ = remaining.Remove(key);
                transitions = remaining;
            }

            return current with { PendingObservations = observations, Pending = pending, PendingTransitions = transitions };
        }, cancellationToken);

    /// <summary>Releases a pass without overwriting a concurrent disposition or replica.</summary>
    public async Task ReleaseAsync(ProcessSweepState owned, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owned);
        for (int attempt = 0; attempt < _options.WriteAttempts; attempt++)
        {
            ReadModelEntry<ProcessSweepState> entry = await ReadAsync(cancellationToken).ConfigureAwait(false);
            ProcessSweepState? current = entry.Value;
            if (current is null || current.Owner != owned.Owner || current.Fence != owned.Fence) return;
            if (await SaveAsync(current with { Owner = null, LeaseUntil = null }, entry.ETag, cancellationToken).ConfigureAwait(false)) return;
        }
    }

    private Task<ReadModelEntry<ProcessSweepState>> ReadAsync(CancellationToken cancellationToken)
        => ReadAsync<ProcessSweepState>(_options.StateStoreName, ProcessStateKeys.Sweep, cancellationToken);

    private Task<bool> SaveAsync(ProcessSweepState state, string? etag, CancellationToken cancellationToken)
    {
        state.Validate(_time);
        return TrySaveAsync(_options.StateStoreName, ProcessStateKeys.Sweep, state, etag ?? string.Empty, cancellationToken);
    }

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

    private static ProcessSweepState Initial() => new(1, 0, 0, false, null, null, null, false, 0, 0, 0, 0, false, true,
        0, new Dictionary<string, int>(StringComparer.Ordinal), new Dictionary<string, ProcessPendingDisposition>(StringComparer.Ordinal), null, null, 0);

    private static ProcessSweepState ResetCycle(ProcessSweepState current, DateTimeOffset now) => current with
    {
        Cycle = checked(current.Cycle + 1),
        Generation = checked(current.Generation + 1),
        Complete = false,
        CycleStartedAt = now,
        CatalogAfterKey = null,
        CatalogComplete = false,
        TenantPage = 0,
        TenantOffset = 0,
        CandidatePage = 0,
        CandidateOffset = 0,
        ExpectedTenantPage = false,
        ExpectedCandidatePage = false,
        TenantProbeMisses = 0,
        CandidateProbeMisses = 0,
        CandidatesComplete = false,
        Unresolved = 0,
        Reasons = new Dictionary<string, int>(StringComparer.Ordinal),
        PendingObservations = new Dictionary<string, ProcessPendingObservation>(StringComparer.Ordinal),
    };
}
