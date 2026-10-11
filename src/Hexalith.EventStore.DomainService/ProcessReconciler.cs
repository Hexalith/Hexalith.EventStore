using Hexalith.EventStore.Client.Effects;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Processes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

/// <summary>Periodic, persisted, fair source and candidate discovery.</summary>
public sealed class ProcessReconciler(
    IServiceScopeFactory scopes,
    ProcessSweepStore sweeps,
    ProcessCheckpointStore checkpoints,
    IOptions<EventStoreProcessOptions> options,
    TimeProvider time) : BackgroundService
{
    private const int LinkedPageFallbackProbes = 4;

    private readonly IServiceScopeFactory _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
    private readonly ProcessSweepStore _sweeps = sweeps ?? throw new ArgumentNullException(nameof(sweeps));
    private readonly ProcessCheckpointStore _checkpoints = checkpoints ?? throw new ArgumentNullException(nameof(checkpoints));
    private readonly EventStoreProcessOptions _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
    private readonly TimeProvider _time = time ?? throw new ArgumentNullException(nameof(time));
    private int _running;
    private string? _localFailureReason;

    /// <summary>Whether the hosted recovery loop is currently live on this replica.</summary>
    public bool IsRunning => Volatile.Read(ref _running) == 1;

    /// <summary>Bounded local evidence from the latest failed hosted pass.</summary>
    public string? LocalFailureReason => Volatile.Read(ref _localFailureReason);

    /// <inheritdoc/>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _scopes.CreateScope();
        IServiceProvider provider = scope.ServiceProvider;
        if (provider.GetService<ProcessRegistration>() is not null)
        {
            _ = provider.GetRequiredService<IProcessSourceCatalog>();
            _ = provider.GetRequiredService<IProcessTranslator>();
            _ = provider.GetRequiredService<IProcessDelegationProvider>();
            _ = provider.GetRequiredService<ITrustedEffectSubmitter>();
            _ = provider.GetRequiredService<IEventStoreGatewayClient>();
            _ = provider.GetRequiredService<IProcessDispositionAuthorizer>();
            _ = provider.GetRequiredService<IProcessDispositionAuditSink>();
            _ = provider.GetRequiredService<ProcessDispositionService>();
        }
        return base.StartAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        Volatile.Write(ref _running, 0);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Runs a bounded pass; all cursor and reason progress survives a restart.</summary>
    public async Task<ProcessReconciliationPass> RunPassAsync(CancellationToken cancellationToken = default)
    {
        // Repair consumes the same finite pass budget as scanning. A one-source
        // pass reserves every third turn for repair so a stuck intent cannot
        // prevent catalog and candidate progress.
        ProcessSweepState beforeRepair = await WithDeadline(ct => _sweeps.GetAsync(ct), cancellationToken).ConfigureAwait(false);
        bool repairTurn = _options.SourcesPerPass > 1 || beforeRepair.Fence % 3 == 0;
        bool repaired = false;
        if (repairTurn)
        {
            if (beforeRepair.NextPublicationRepair)
                repaired = await RecoverOnePublicationAsync(cancellationToken).ConfigureAwait(false)
                    || await RecoverOneDispositionAsync(cancellationToken).ConfigureAwait(false);
            else
                repaired = await RecoverOneDispositionAsync(cancellationToken).ConfigureAwait(false)
                    || await RecoverOnePublicationAsync(cancellationToken).ConfigureAwait(false);
        }
        ProcessSweepState? state = await WithDeadline(ct => _sweeps.AcquireAsync(ct), cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            ProcessSweepState current = await WithDeadline(ct => _sweeps.GetAsync(ct), cancellationToken).ConfigureAwait(false);
            return new ProcessReconciliationPass(0, current.Complete, current.Unresolved);
        }

        int examined = 0;
        try
        {
            ProcessPendingObservation[] pendingObservations = [.. state.PendingObservations.Values];
            int pendingStart = pendingObservations.Length == 0 ? 0 : (int)(state.Fence % pendingObservations.Length);
            int budget = _options.SourcesPerPass - (repaired ? 1 : 0);
            int observationBudget = Math.Min(pendingObservations.Length, 1);
            foreach (ProcessPendingObservation pending in pendingObservations.Skip(pendingStart)
                .Concat(pendingObservations.Take(pendingStart)).Take(observationBudget))
            {
                state = await FinishObservationAsync(state, pending, cancellationToken).ConfigureAwait(false);
            }

            using IServiceScope scope = _scopes.CreateScope();
            IProcessSourceCatalog catalog = scope.ServiceProvider.GetRequiredService<IProcessSourceCatalog>();
            CheckpointedProcessRunner runner = scope.ServiceProvider.GetRequiredService<CheckpointedProcessRunner>();
            int work = 0;
            ProcessSourcePage? catalogPage = null;
            int catalogOffset = 0;
            while (work < budget && state.PendingObservations.Count < 4096
                && state.SerializedSize() < ProcessSweepState.MaxSerializedBytes - 8192
                && (!state.CatalogComplete || !state.CandidatesComplete))
            {
                bool catalogTurn = state.NextCatalog && !state.CatalogComplete || state.CandidatesComplete;
                if (catalogTurn)
                {
                    (state, bool observed, catalogPage, catalogOffset) = await ScanCatalogAsync(
                        state, catalog, runner, catalogPage, catalogOffset, budget - work, cancellationToken).ConfigureAwait(false);
                    if (observed) examined++;
                }
                else
                {
                    (state, bool observed) = await ScanCandidateAsync(state, runner, cancellationToken).ConfigureAwait(false);
                    if (observed) examined++;
                }

                work++;
            }

            if (state.SerializedSize() >= ProcessSweepState.MaxSerializedBytes - 8192
                && !state.Reasons.ContainsKey("sweep-record-budget"))
                state = await _sweeps.UpdateOwnedAsync(state, current => AddReason(current, "sweep-record-budget"), cancellationToken).ConfigureAwait(false);

            if (state.CatalogComplete && state.CandidatesComplete && state.CapacityExceeded
                && !state.Reasons.ContainsKey("capacity-exceeded"))
            {
                state = await _sweeps.UpdateOwnedAsync(state, current => AddReason(current, "capacity-exceeded"), cancellationToken).ConfigureAwait(false);
            }

            if (state.CatalogComplete && state.CandidatesComplete && state.Pending.Count == 0
                && state.PendingObservations.Count == 0 && state.PendingPublications.Count == 0 && state.PendingTransitions.Count == 0)
            {
                state = await _sweeps.UpdateOwnedAsync(state, current => current with
                {
                    Complete = true,
                    LastCompleteAt = _time.GetUtcNow(),
                    CatalogAfterKey = null,
                    TenantPage = 0,
                    TenantOffset = 0,
                    CandidatePage = 0,
                    CandidateOffset = 0,
                    ExpectedTenantPage = false,
                    ExpectedCandidatePage = false,
                    TenantProbeMisses = 0,
                    CandidateProbeMisses = 0,
                }, cancellationToken).ConfigureAwait(false);
            }

            return new ProcessReconciliationPass(examined, state.Complete, state.Unresolved);
        }
        finally
        {
            await _sweeps.ReleaseAsync(state, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task<(ProcessSweepState State, bool Observed, ProcessSourcePage? Page, int Offset)> ScanCatalogAsync(
        ProcessSweepState state, IProcessSourceCatalog catalog, CheckpointedProcessRunner runner,
        ProcessSourcePage? page, int offset, int remainingBudget, CancellationToken cancellationToken)
    {
        try
        {
            if (page is null)
            {
                int requestSize = Math.Min(_options.DiscoveryPageSize, remainingBudget);
                page = await WithReadDeadline(ct => catalog.ListAsync(state.CatalogAfterKey, requestSize, ct), cancellationToken).ConfigureAwait(false);
                if (page is null || page.Sources is null || page.Sources.Count > requestSize
                    || page.Sources.Count == 0 && page.NextKey is not null)
                    throw new InvalidOperationException("Process catalog page is invalid.");
                string? previousKey = state.CatalogAfterKey;
                foreach (ProcessSource entry in page.Sources)
                {
                    entry.Validate();
                    if (previousKey is not null && string.CompareOrdinal(entry.CatalogKey, previousKey) <= 0)
                        throw new InvalidOperationException("Process catalog continuation is not a strict keyset.");
                    previousKey = entry.CatalogKey;
                }
                if (page.NextKey is not null && page.NextKey != previousKey)
                    throw new InvalidOperationException("Process catalog continuation is not a strict keyset.");
                offset = 0;
            }

            if (offset < page.Sources.Count)
            {
                ProcessSource source = page.Sources[offset];
                try
                {
                    if (!await _checkpoints.EnsureCandidateAsync(source, state, cancellationToken).ConfigureAwait(false))
                    {
                        state = await RecordSourceFailureAsync(state, source, "candidate-publication", cancellationToken).ConfigureAwait(false);
                        return await ContinueAfterPublicationFailureAsync(state, source, page, offset, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
                {
                    // A publication intent remains durable; do not move past its catalog key.
                    state = await RecordSourceFailureAsync(state, source, "candidate-publication", cancellationToken).ConfigureAwait(false);
                    return await ContinueAfterPublicationFailureAsync(state, source, page, offset, cancellationToken).ConfigureAwait(false);
                }
                try
                {
                    state = await ObserveAsync(state, source, runner, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
                {
                    state = await RecordSourceFailureAsync(state, source, "source-state-unavailable", cancellationToken).ConfigureAwait(false);
                }
                state = await _sweeps.UpdateOwnedAsync(state, current => current with
                {
                    CatalogAfterKey = offset + 1 == page.Sources.Count ? page.NextKey : source.CatalogKey,
                    CatalogComplete = offset + 1 == page.Sources.Count && page.NextKey is null,
                    NextCatalog = false,
                }, cancellationToken).ConfigureAwait(false);
                offset++;
                return (state, true, offset == page.Sources.Count ? null : page, offset);
            }

            state = await _sweeps.UpdateOwnedAsync(state, current => current with { CatalogComplete = true, NextCatalog = false }, cancellationToken).ConfigureAwait(false);
            return (state, false, null, 0);
        }
        catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
        {
            // Keep the catalog cursor for the next cycle, while serving retained candidates now.
            string reason = exception is ProcessReadAdmissionException ? "read-worker-exhausted" : "catalog-unavailable";
            state = await RecordScanFailureAsync(state, reason, current => current with { CatalogComplete = true, NextCatalog = false }, cancellationToken).ConfigureAwait(false);
            return (state, false, null, 0);
        }
    }

    private async Task<(ProcessSweepState State, bool Observed, ProcessSourcePage? Page, int Offset)> ContinueAfterPublicationFailureAsync(
        ProcessSweepState state, ProcessSource source, ProcessSourcePage page, int offset, CancellationToken cancellationToken)
    {
        // The durable intent owns recovery after catalog removal, so this entry may yield
        // to later catalog keys. A failed admission has no intent and must stay at this key.
        bool retained = await _sweeps.HasPendingPublicationAsync(source, cancellationToken).ConfigureAwait(false);
        state = await _sweeps.UpdateOwnedAsync(state, current => current with
        {
            CatalogAfterKey = retained ? source.CatalogKey : current.CatalogAfterKey,
            CatalogComplete = retained && offset + 1 == page.Sources.Count && page.NextKey is null,
            NextCatalog = false,
        }, cancellationToken).ConfigureAwait(false);
        int nextOffset = retained ? offset + 1 : offset;
        return (state, false, retained && nextOffset == page.Sources.Count ? null : page, nextOffset);
    }

    private async Task<(ProcessSweepState State, bool Observed)> ScanCandidateAsync(ProcessSweepState state, CheckpointedProcessRunner runner, CancellationToken cancellationToken)
    {
        ProcessIndexPage<string>? tenants;
        try
        {
            tenants = await WithDeadline(ct => _checkpoints.ReadTenantPageAsync(state.TenantPage, ct), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
        {
            int? tail = null;
            try
            {
                tail = await WithDeadline(ct => _checkpoints.ReadTenantTailAsync(ct), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception tailException) when (IsRecoverable(tailException, cancellationToken))
            {
                // The page failure remains unresolved; a finite probe yields to other work.
            }
            bool probe = tail is { } lastPage && state.TenantPage < lastPage
                || state.TenantProbeMisses < LinkedPageFallbackProbes;
            state = await RecordScanFailureAsync(state, "tenant-page-unreadable", current => current with
            {
                TenantPage = probe ? current.TenantPage + 1 : 0, TenantOffset = 0, CandidatePage = 0, CandidateOffset = 0,
                ExpectedTenantPage = probe, ExpectedCandidatePage = false,
                TenantProbeMisses = probe ? checked(current.TenantProbeMisses + 1) : 0,
                CandidateProbeMisses = 0, CandidatesComplete = !probe, NextCatalog = true,
            }, cancellationToken).ConfigureAwait(false);
            return (state, false);
        }

        if (tenants is null)
        {
            int? tail;
            try
            {
                tail = await WithDeadline(ct => _checkpoints.ReadTenantTailAsync(ct), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
            {
                bool probe = state.TenantProbeMisses < LinkedPageFallbackProbes;
                state = await RecordScanFailureAsync(state, "tenant-tail-unreadable", current => current with
                {
                    TenantPage = probe ? current.TenantPage + 1 : 0,
                    TenantOffset = 0, CandidatePage = 0, CandidateOffset = 0,
                    TenantProbeMisses = probe ? current.TenantProbeMisses + 1 : 0,
                    CandidateProbeMisses = 0, ExpectedTenantPage = probe, ExpectedCandidatePage = false,
                    CandidatesComplete = !probe, NextCatalog = true,
                }, cancellationToken).ConfigureAwait(false);
                return (state, false);
            }
            bool missing = state.TenantPage <= tail || state.ExpectedTenantPage
                || state.TenantPage == 0 && state.RetainedCandidates > state.PendingPublications.Count;
            bool later = tail is { } lastTenantPage && state.TenantPage < lastTenantPage
                || state.ExpectedTenantPage && state.TenantProbeMisses < LinkedPageFallbackProbes;
            if (missing)
            {
                state = await RecordScanFailureAsync(state, "tenant-page-missing", current => current with
                {
                    TenantPage = later ? current.TenantPage + 1 : 0,
                    TenantOffset = 0,
                    CandidatePage = 0, CandidateOffset = 0,
                    TenantProbeMisses = later ? checked(current.TenantProbeMisses + 1) : 0,
                    CandidateProbeMisses = 0,
                    CandidatesComplete = !later, NextCatalog = true,
                    ExpectedTenantPage = later, ExpectedCandidatePage = false,
                }, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                state = await _sweeps.UpdateOwnedAsync(state, current => current with
                {
                    CandidatesComplete = true, TenantPage = 0, TenantOffset = 0, CandidatePage = 0, CandidateOffset = 0,
                    ExpectedTenantPage = false, ExpectedCandidatePage = false, TenantProbeMisses = 0, CandidateProbeMisses = 0,
                    NextCatalog = true,
                }, cancellationToken).ConfigureAwait(false);
            }
            return (state, false);
        }

        if (state.TenantOffset > tenants.Entries.Count)
        {
            state = await RecordScanFailureAsync(state, "tenant-offset-invalid", current => current with
            {
                TenantOffset = tenants.Entries.Count, CandidatePage = 0, CandidateOffset = 0, NextCatalog = true,
            }, cancellationToken).ConfigureAwait(false);
            return (state, false);
        }

        if (state.TenantOffset >= tenants.Entries.Count)
        {
            if (tenants.NextPage is null)
            {
                int? tail;
                try
                {
                    tail = await WithDeadline(ct => _checkpoints.ReadTenantTailAsync(ct), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
                {
                    state = await RecordScanFailureAsync(state, "tenant-tail-unreadable", current => current with
                    {
                        TenantPage = current.TenantPage + 1, TenantOffset = 0, CandidatePage = 0, CandidateOffset = 0,
                        ExpectedTenantPage = true, ExpectedCandidatePage = false,
                        TenantProbeMisses = 0, CandidateProbeMisses = 0, NextCatalog = true,
                    }, cancellationToken).ConfigureAwait(false);
                    return (state, false);
                }
                if (tail > state.TenantPage)
                {
                    state = await RecordScanFailureAsync(state, "tenant-tail-unlinked", current => current with
                    {
                        TenantPage = current.TenantPage + 1, TenantOffset = 0, CandidatePage = 0, CandidateOffset = 0,
                        ExpectedTenantPage = true, ExpectedCandidatePage = false, TenantProbeMisses = 0,
                        CandidateProbeMisses = 0, NextCatalog = true,
                    }, cancellationToken).ConfigureAwait(false);
                    return (state, false);
                }
            }
            state = await _sweeps.UpdateOwnedAsync(state, current => tenants.NextPage is null
                ? current with { CandidatesComplete = true, TenantPage = 0, TenantOffset = 0, CandidatePage = 0, CandidateOffset = 0,
                    ExpectedTenantPage = false, ExpectedCandidatePage = false, TenantProbeMisses = 0, CandidateProbeMisses = 0, NextCatalog = true }
                : current with { TenantPage = tenants.NextPage.Value, TenantOffset = 0, CandidatePage = 0, CandidateOffset = 0,
                    ExpectedTenantPage = true, ExpectedCandidatePage = false, TenantProbeMisses = 0, CandidateProbeMisses = 0, NextCatalog = true }, cancellationToken).ConfigureAwait(false);
            return (state, false);
        }

        string tenant = tenants.Entries[state.TenantOffset];
        ProcessIndexPage<ProcessSource>? candidates;
        try
        {
            candidates = await WithDeadline(ct => _checkpoints.ReadCandidatePageAsync(tenant, state.CandidatePage, ct), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
        {
            // Sequential page keys let the next page be probed even when this page cannot be decoded.
            int? tail = null;
            try
            {
                tail = await WithDeadline(ct => _checkpoints.ReadCandidateTailAsync(tenant, ct), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception tailException) when (IsRecoverable(tailException, cancellationToken))
            {
                // The unreadable page remains unresolved while the finite fallback advances.
            }
            bool probe = tail is { } lastPage && state.CandidatePage < lastPage
                || state.CandidateProbeMisses < LinkedPageFallbackProbes;
            state = await RecordScanFailureAsync(state, "candidate-page-unreadable", current => current with
            {
                CandidatePage = probe ? current.CandidatePage + 1 : 0, CandidateOffset = 0,
                TenantOffset = probe ? current.TenantOffset : current.TenantOffset + 1,
                ExpectedCandidatePage = probe,
                CandidateProbeMisses = probe ? checked(current.CandidateProbeMisses + 1) : 0, NextCatalog = true,
            }, cancellationToken).ConfigureAwait(false);
            return (state, false);
        }

        if (candidates is null)
        {
            int? tail;
            try
            {
                tail = await WithDeadline(ct => _checkpoints.ReadCandidateTailAsync(tenant, ct), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
            {
                bool probe = state.CandidateProbeMisses < LinkedPageFallbackProbes;
                state = await RecordScanFailureAsync(state, "candidate-tail-unreadable", current => current with
                {
                    CandidatePage = probe ? current.CandidatePage + 1 : 0,
                    CandidateOffset = 0, TenantOffset = probe ? current.TenantOffset : current.TenantOffset + 1,
                    CandidateProbeMisses = probe ? current.CandidateProbeMisses + 1 : 0,
                    ExpectedCandidatePage = probe, NextCatalog = true,
                }, cancellationToken).ConfigureAwait(false);
                return (state, false);
            }
            bool missing = state.CandidatePage <= tail || state.ExpectedCandidatePage || state.CandidatePage == 0;
            bool later = tail is { } lastCandidatePage && state.CandidatePage < lastCandidatePage
                || state.ExpectedCandidatePage && state.CandidateProbeMisses < LinkedPageFallbackProbes;
            if (missing)
            {
                state = await RecordScanFailureAsync(state, "candidate-page-missing", current => current with
                {
                    CandidatePage = later ? current.CandidatePage + 1 : 0,
                    CandidateOffset = 0,
                    TenantOffset = later ? current.TenantOffset : current.TenantOffset + 1,
                    ExpectedCandidatePage = later,
                    CandidateProbeMisses = later ? checked(current.CandidateProbeMisses + 1) : 0, NextCatalog = true,
                }, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                state = await _sweeps.UpdateOwnedAsync(state, current => current with
                {
                    TenantOffset = current.TenantOffset + 1, CandidatePage = 0, CandidateOffset = 0,
                    ExpectedCandidatePage = false, CandidateProbeMisses = 0, NextCatalog = true,
                }, cancellationToken).ConfigureAwait(false);
            }
            return (state, false);
        }

        if (state.CandidateOffset > candidates.Entries.Count)
        {
            state = await RecordScanFailureAsync(state, "candidate-offset-invalid", current => current with
            {
                CandidateOffset = candidates.Entries.Count, NextCatalog = true,
            }, cancellationToken).ConfigureAwait(false);
            return (state, false);
        }

        if (state.CandidateOffset >= candidates.Entries.Count)
        {
            if (candidates.NextPage is null)
            {
                int? tail;
                try
                {
                    tail = await WithDeadline(ct => _checkpoints.ReadCandidateTailAsync(tenant, ct), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
                {
                    state = await RecordScanFailureAsync(state, "candidate-tail-unreadable", current => current with
                    {
                        CandidatePage = current.CandidatePage + 1, CandidateOffset = 0,
                        ExpectedCandidatePage = true, CandidateProbeMisses = 0, NextCatalog = true,
                    }, cancellationToken).ConfigureAwait(false);
                    return (state, false);
                }
                if (tail > state.CandidatePage)
                {
                    state = await RecordScanFailureAsync(state, "candidate-tail-unlinked", current => current with
                    {
                        CandidatePage = current.CandidatePage + 1, CandidateOffset = 0,
                        ExpectedCandidatePage = true, CandidateProbeMisses = 0, NextCatalog = true,
                    }, cancellationToken).ConfigureAwait(false);
                    return (state, false);
                }
            }
            state = await _sweeps.UpdateOwnedAsync(state, current => candidates.NextPage is null
                ? current with { TenantOffset = current.TenantOffset + 1, CandidatePage = 0, CandidateOffset = 0,
                    ExpectedCandidatePage = false, CandidateProbeMisses = 0, NextCatalog = true }
                : current with { CandidatePage = candidates.NextPage.Value, CandidateOffset = 0,
                    ExpectedCandidatePage = true, CandidateProbeMisses = 0, NextCatalog = true }, cancellationToken).ConfigureAwait(false);
            return (state, false);
        }

        ProcessSource candidate = candidates.Entries[state.CandidateOffset];
        try
        {
            state = await ObserveAsync(state, candidate, runner, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
        {
            state = await RecordSourceFailureAsync(state, candidate, "source-state-unavailable", cancellationToken).ConfigureAwait(false);
        }
        state = await _sweeps.UpdateOwnedAsync(state, current => current with { CandidateOffset = current.CandidateOffset + 1, NextCatalog = true }, cancellationToken).ConfigureAwait(false);
        return (state, true);
    }

    private async Task<ProcessSweepState> ObserveAsync(ProcessSweepState state, ProcessSource source, CheckpointedProcessRunner runner, CancellationToken cancellationToken)
    {
        string key = ProcessStateKeys.SourceId(source);
        if (state.PendingObservations.TryGetValue(key, out ProcessPendingObservation? pending))
            return await FinishObservationAsync(state, pending, cancellationToken).ConfigureAwait(false);

        ProcessSourceState before = await WithDeadline(ct => _checkpoints.GetAsync(source, ct), cancellationToken).ConfigureAwait(false);
        bool transitionNeedsCoverage = state.PendingTransitions.TryGetValue(key, out ProcessPendingTransition? transition)
            && before.LastTransitionToken == transition.Token;
        if (before.ObservedCycle == state.Cycle && !transitionNeedsCoverage)
            return await _sweeps.CoverAsync(state, source, cancellationToken).ConfigureAwait(false);

        ProcessRunResult result;
        try
        {
            result = await RunWithSweepRenewalAsync(state, source, runner, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
        {
            return await RecordSourceFailureAsync(state, source,
                exception is ProcessReadAdmissionException ? "read-worker-exhausted" : "source-operation", cancellationToken).ConfigureAwait(false);
        }

        ProcessSourceState observation = await WithDeadline(ct => _checkpoints.GetAsync(source, ct), cancellationToken).ConfigureAwait(false);
        bool unresolved = result.Unresolved || observation.Status != ProcessSourceStatus.Active;
        bool alreadyUnresolvedThisCycle = observation.ObservedCycle == state.Cycle && observation.ObservedUnresolved;
        bool needsMarker = observation.ObservedCycle < state.Cycle
            || unresolved && !alreadyUnresolvedThisCycle
            || state.PendingTransitions.TryGetValue(key, out transition)
                && observation.LastTransitionToken == transition.Token
            || state.PendingObservations.ContainsKey(key);
        state = await _sweeps.UpdateOwnedAsync(state, current =>
        {
            if (!needsMarker) return current;
            current.PendingObservations.TryGetValue(key, out ProcessPendingObservation? pending);
            if (pending is null && current.PendingObservations.Count >= 4096)
                throw new InvalidOperationException("Process pending-observation budget exhausted.");
            bool countUnresolved = unresolved && !alreadyUnresolvedThisCycle && pending?.Unresolved != true;
            string reason = result.Status == ProcessSourceStatus.Incomplete && observation.Status == ProcessSourceStatus.Active
                ? result.ReasonCode ?? "source-incomplete"
                : observation.ReasonCode is { Length: > 0 } value && value.Length <= 64 ? value : observation.Status.ToString();
            ProcessSweepState counted = countUnresolved ? AddReason(current, reason) : current;
            return counted with
            {
                PendingObservations = new Dictionary<string, ProcessPendingObservation>(current.PendingObservations, StringComparer.Ordinal)
                {
                    [key] = new ProcessPendingObservation(source, current.Cycle,
                        unresolved || alreadyUnresolvedThisCycle || pending?.Unresolved == true),
                },
            };
        }, cancellationToken).ConfigureAwait(false);
        if (needsMarker) return await FinishObservationAsync(state, state.PendingObservations[key], cancellationToken).ConfigureAwait(false);
        return await _sweeps.CoverAsync(state, source, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProcessSweepState> RecordSourceFailureAsync(ProcessSweepState state, ProcessSource source, string reason, CancellationToken cancellationToken)
    {
        string key = ProcessStateKeys.SourceId(source);
        if (state.PendingObservations.TryGetValue(key, out ProcessPendingObservation? pending))
            return await FinishObservationAsync(state, pending, cancellationToken).ConfigureAwait(false);
        try
        {
            ProcessSourceState observed = await WithDeadline(ct => _checkpoints.GetAsync(source, ct), cancellationToken).ConfigureAwait(false);
            if (observed.ObservedCycle == state.Cycle && observed.ObservedUnresolved)
                return await _sweeps.CoverAsync(state, source, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
        {
            // Corrupt source state cannot provide a usable cycle marker. The pending
            // observation below keeps the reason durable until its marker is repaired.
        }
        state = await _sweeps.UpdateOwnedAsync(state, current =>
        {
            if (current.PendingObservations.ContainsKey(key)) return current;
            if (current.PendingObservations.Count >= 4096)
                throw new InvalidOperationException("Process pending-observation budget exhausted.");
            ProcessSweepState counted = AddReason(current, reason);
            return counted with
            {
                PendingObservations = new Dictionary<string, ProcessPendingObservation>(current.PendingObservations, StringComparer.Ordinal)
                {
                    [key] = new ProcessPendingObservation(source, current.Cycle, true),
                },
            };
        }, cancellationToken).ConfigureAwait(false);
        return await FinishObservationAsync(state, state.PendingObservations[key], cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProcessRunResult> RunWithSweepRenewalAsync(ProcessSweepState state, ProcessSource source, CheckpointedProcessRunner runner, CancellationToken cancellationToken)
    {
        using var work = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task renewal = RenewSweepWhileRunningAsync(state, work);
        try
        {
            return await runner.RunWithSweepAsync(source, state, work.Token).ConfigureAwait(false);
        }
        finally
        {
            work.Cancel();
            await renewal.ConfigureAwait(false);
        }
    }

    private async Task RenewSweepWhileRunningAsync(ProcessSweepState state, CancellationTokenSource work)
    {
        try
        {
            TimeSpan interval = TimeSpan.FromTicks(Math.Max(TimeSpan.FromMilliseconds(10).Ticks, _options.LeaseDuration.Ticks / 3));
            while (!work.IsCancellationRequested)
            {
                await Task.Delay(interval, _time, work.Token).ConfigureAwait(false);
                _ = await _sweeps.UpdateOwnedAsync(state, static current => current, work.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (work.IsCancellationRequested)
        {
        }
        catch
        {
            work.Cancel();
            throw;
        }
    }

    private async Task<ProcessSweepState> FinishObservationAsync(ProcessSweepState state, ProcessPendingObservation pending, CancellationToken cancellationToken)
    {
        try
        {
            ProcessSourceState observed = await WithDeadline(ct => _checkpoints.GetAsync(pending.Source, ct), cancellationToken).ConfigureAwait(false);
            _ = await WithDeadline(ct => _checkpoints.MarkObservedAsync(pending.Source, pending.Cycle, pending.Unresolved, ct), cancellationToken).ConfigureAwait(false);
            return await _sweeps.FinishObservationAsync(state, pending.Source, observed.LastTransitionToken, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
        {
            // The sweep already counted this source. Retain the handoff for the next pass.
            return state;
        }
    }

    private Task<ProcessSweepState> RecordScanFailureAsync(ProcessSweepState state, string reason, Func<ProcessSweepState, ProcessSweepState> move, CancellationToken cancellationToken)
        => _sweeps.UpdateOwnedAsync(state, current => move(AddReason(current, reason)), cancellationToken);

    private static ProcessSweepState AddReason(ProcessSweepState current, string reason)
    {
        var reasons = new Dictionary<string, int>(current.Reasons, StringComparer.Ordinal);
        if (!reasons.ContainsKey(reason) && reasons.Count >= 15) reason = "other";
        reasons[reason] = reasons.GetValueOrDefault(reason) + 1;
        return current with { Unresolved = checked(current.Unresolved + 1), Reasons = reasons };
    }

    private async Task<bool> RecoverOneDispositionAsync(CancellationToken cancellationToken)
    {
        ProcessSweepState current = await WithDeadline(ct => _sweeps.GetAsync(ct), cancellationToken).ConfigureAwait(false);
        string[] keys = [.. current.Pending.Where(static pair => !pair.Value.Applied).Select(static pair => pair.Key).OrderBy(static key => key, StringComparer.Ordinal)];
        if (keys.Length == 0) return false;
        string key = keys.FirstOrDefault(candidate => string.CompareOrdinal(candidate, current.DispositionRepairAfterKey) > 0) ?? keys[0];
        ProcessPendingDisposition pending = current.Pending[key];
        await _sweeps.AdvanceRepairAsync(key, false, cancellationToken).ConfigureAwait(false);
        try
        {
            _ = await WithRepairDeadline(async ct =>
            {
                ProcessPendingDisposition? claim = await _sweeps.ClaimDispositionAsync(pending.Source, pending.PreparedGeneration, ct).ConfigureAwait(false);
                if (claim is not null && await _checkpoints.RevalidateAsync(claim, ct).ConfigureAwait(false))
                    _ = await _sweeps.MarkDispositionAppliedAsync(claim, ct).ConfigureAwait(false);
                return true;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
        {
            // Prepared evidence stays durable and readiness stays degraded for the next pass.
        }
        return true;
    }

    private async Task<bool> RecoverOnePublicationAsync(CancellationToken cancellationToken)
    {
        ProcessSweepState current = await WithDeadline(ct => _sweeps.GetAsync(ct), cancellationToken).ConfigureAwait(false);
        string[] keys = [.. current.PendingPublications.Keys.OrderBy(static key => key, StringComparer.Ordinal)];
        if (keys.Length == 0) return false;
        string key = keys.FirstOrDefault(candidate => string.CompareOrdinal(candidate, current.PublicationRepairAfterKey) > 0) ?? keys[0];
        ProcessSource source = current.PendingPublications[key];
        await _sweeps.AdvanceRepairAsync(key, true, cancellationToken).ConfigureAwait(false);
        try
        {
            _ = await WithRepairDeadline(ct => _checkpoints.EnsureCandidateAsync(source, current, ct), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
        {
            // The exact source identity stays in the sweep for a later retry.
        }
        return true;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.ReconciliationEnabled) return;
        Volatile.Write(ref _running, 1);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _ = await RunPassAsync(stoppingToken).ConfigureAwait(false);
                    Volatile.Write(ref _localFailureReason, null);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    Volatile.Write(ref _localFailureReason,
                        exception is ProcessReadAdmissionException ? "read-worker-exhausted" : "hosted-pass-failed");
                }

                try
                {
                    await Task.Delay(_options.ReconciliationInterval, _time, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }
    }

    private async Task<T> WithDeadline<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_options.OperationTimeout);
        return await operation(deadline.Token).WaitAsync(_options.OperationTimeout, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> WithRepairDeadline<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        TimeSpan quantum = _options.OperationTimeout * 16;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(quantum);
        return await operation(deadline.Token).WaitAsync(quantum, cancellationToken).ConfigureAwait(false);
    }

    private async Task WithDeadline(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_options.OperationTimeout);
        await operation(deadline.Token).WaitAsync(_options.OperationTimeout, cancellationToken).ConfigureAwait(false);
    }

    private Task<T> WithReadDeadline<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
        => ProcessReadDeadline.RunAsync(operation, _options.OperationTimeout, cancellationToken);

    private static bool IsRecoverable(Exception exception, CancellationToken cancellationToken)
        => exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested;
}
