using Hexalith.EventStore.Client.Reminders;
using Hexalith.EventStore.Client.Processes;
using Hexalith.EventStore.Contracts.Reminders;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Periodic reminder reconciliation. The first pass starts with the host; later passes follow
/// <see cref="EventStoreReminderOptions.ReconciliationInterval"/>, or the shorter retry delay after an
/// incomplete pass caused by unreadable work. Capacity alone keeps the normal cadence while degrading readiness.
/// Each pass walks the discovery index and converges every candidate from its stream, so a
/// lost firing is reissued when due and a deleted scheduler reminder is re-armed when still in the future.
/// </summary>
internal sealed class ReminderReconciler(
    ReminderIntentIndex index,
    IReminderRegistrar registrar,
    ReminderRuntimeStatus status,
    IOptions<EventStoreReminderOptions> options,
    TimeProvider timeProvider,
    ILogger<ReminderReconciler> logger,
    IServiceScopeFactory? scopes = null) : BackgroundService
{
    private readonly ReminderIntentIndex _index = index ?? throw new ArgumentNullException(nameof(index));
    private readonly IReminderRegistrar _registrar = registrar ?? throw new ArgumentNullException(nameof(registrar));
    private readonly ReminderRuntimeStatus _status = status ?? throw new ArgumentNullException(nameof(status));
    private readonly EventStoreReminderOptions _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
    private readonly TimeProvider _time = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly ILogger<ReminderReconciler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>Runs one complete pass over the discovery index.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The pass counts.</returns>
    public async Task<ReminderReconciliationPass> RunPassAsync(CancellationToken cancellationToken)
    {
        long passVersion = _status.BeginPass();
        var observed = new List<string>();
        int tenants = 0;
        int candidates = 0;
        int armed = 0;
        int submitted = 0;
        int cancelled = 0;
        int unresolved = 0;
        int quarantined = 0;
        int incomplete = 0;
        int capacityLimitedTenants = 0;

        // Indexed and initial discovery each receive a finite independent service slice.
        // The cursor advances after every attempted target; failures survive until a fresh full sweep.
        ReminderCatalogDiscoveryState? indexed = null;
        string? indexedAfter = null;
        bool indexedUnavailable = false;
        bool indexedComplete = false;
        bool indexedBudgetYielded = false;
        int indexedIssues = 0;
        long indexedStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        TimeSpan IndexedRemaining()
        {
            TimeSpan remaining = _options.CatalogServiceBudget - System.Diagnostics.Stopwatch.GetElapsedTime(indexedStarted);
            if (remaining <= TimeSpan.Zero) { indexedBudgetYielded = true; throw new TimeoutException(); }
            return remaining < _options.CatalogOperationTimeout ? remaining : _options.CatalogOperationTimeout;
        }
        try
        {
            indexed = await ProcessReadDeadline.RunControlAsync(ct => _index.ClaimDiscoveryAsync(true, ct),
                IndexedRemaining(), cancellationToken).ConfigureAwait(false);
            indexedAfter = indexed.AfterKey;
            _status.BeginIndexedSweep(indexed);
            indexedUnavailable = indexed.IsIncomplete;
            if (indexedUnavailable) indexedIssues++;
            string? afterTenant = null;
            string? afterActor = null;
            if (indexedAfter is not null)
            {
                string[] parts = indexedAfter.Split('\0');
                if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                    throw new InvalidOperationException("Reminder indexed continuation is invalid.");
                afterTenant = parts[0];
                afterActor = parts[1];
            }
            IReadOnlyList<string> tenantList = await ProcessReadDeadline.RunControlAsync(ct => _index.ListTenantsAsync(ct),
                IndexedRemaining(), cancellationToken).ConfigureAwait(false);
            int attempts = 0;
            bool yielded = false;
            foreach (string tenant in tenantList.Order(StringComparer.Ordinal))
            {
                if (afterTenant is not null && string.CompareOrdinal(tenant, afterTenant) < 0) continue;
                if (tenant == afterTenant && afterActor == "~") continue;
                if (System.Diagnostics.Stopwatch.GetElapsedTime(indexedStarted) >= _options.CatalogServiceBudget) { yielded = true; break; }
                tenants++;
                IReadOnlyList<ReminderCandidate> tenantCandidates;
                try
                {
                    tenantCandidates = await ProcessReadDeadline.RunControlAsync(ct => _index.ListCandidatesAsync(tenant, ct),
                        IndexedRemaining(), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception exception)
                {
                    indexedUnavailable = true;
                    indexedIssues++;
                    indexedAfter = tenant + "\0~";
                    indexed = await WriteOwnedAsync(true, indexed, false, indexedAfter, true,
                        IndexedRemaining(), cancellationToken).ConfigureAwait(false);
                    ReminderLog.ScanFailed(_logger, "tenant-candidates", FailureReason(exception), exception.GetType().Name);
                    continue;
                }
                if (tenantCandidates.Count >= _options.MaxCandidatesPerTenant)
                {
                    capacityLimitedTenants++;
                    indexedIssues++;
                    indexedUnavailable = true;
                }
                foreach (ReminderCandidate? candidate in tenantCandidates.OrderBy(candidate => candidate?.ActorId, StringComparer.Ordinal))
                {
                    if (candidate is not null && tenant == afterTenant && string.CompareOrdinal(candidate.ActorId, afterActor) <= 0) continue;
                    if (attempts >= _options.CatalogPagesPerPass * _options.CatalogPageSize
                        || System.Diagnostics.Stopwatch.GetElapsedTime(indexedStarted) >= _options.CatalogServiceBudget)
                    { yielded = true; break; }
                    attempts++;
                    candidates++;
                    string actor = "candidate-invalid";
                    try
                    {
                        if (candidate is null) throw new ReminderFailClosedException("candidate-missing");
                        var target = new ReminderTarget(tenant, candidate.Domain, candidate.Aggregate);
                        actor = ReminderCoordinator.ComputeActorId(target);
                        if (!string.Equals(actor, candidate.ActorId, StringComparison.Ordinal))
                            throw new ReminderFailClosedException("actor-id-mismatch");
                        observed.Add(actor);
                        ReminderConvergenceResult result = await ProcessReadDeadline.RunConvergenceAsync(_index.AdmissionScope, actor,
                            ct => _registrar.ConvergeAsync(target, ct), IndexedRemaining(), cancellationToken).ConfigureAwait(false);
                        armed += result.Armed;
                        submitted += result.Submitted;
                        cancelled += result.Cancelled;
                        unresolved += result.Unresolved;
                        quarantined += result.Quarantined;
                        indexedUnavailable |= result.Unresolved > 0 || result.Quarantined > 0;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch (Exception exception)
                    {
                        indexedUnavailable = true;
                        indexedIssues++;
                        ReminderLog.CandidateFailed(_logger, actor, FailureReason(exception),
                            exception is ReminderFailClosedException { ReasonCode: "actor-id-mismatch" or "candidate-missing" } ? string.Empty : exception.GetType().Name);
                    }
                    if (candidate is not null) indexedAfter = tenant + "\0" + candidate.ActorId;
                    indexed = await WriteOwnedAsync(true, indexed, false, indexedAfter, indexedUnavailable,
                        IndexedRemaining(), cancellationToken).ConfigureAwait(false);
                }
                if (yielded) break;
                indexedAfter = tenant + "\0~";
                indexed = await WriteOwnedAsync(true, indexed, false, indexedAfter, indexedUnavailable,
                    IndexedRemaining(), cancellationToken).ConfigureAwait(false);
            }
            indexedComplete = !yielded && !indexedUnavailable;
            indexed = await WriteOwnedAsync(true, indexed, indexedComplete, yielded ? indexedAfter : null, indexedUnavailable,
                IndexedRemaining(), cancellationToken).ConfigureAwait(false);
            incomplete += indexedIssues;
            if (yielded && indexedIssues == 0) incomplete++;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            incomplete++;
            if (indexed is not null)
            {
                try
                {
                    await WriteOwnedAsync(true, indexed, false, indexedAfter, indexedUnavailable || !indexedBudgetYielded,
                        TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception) { /* An unfinished or newer owned transition remains authoritative. */ }
            }
            ReminderLog.ScanFailed(_logger, "tenant-registry", FailureReason(exception), exception.GetType().Name);
        }

        // Existing witnesses always receive service before catalog reads. Discovery then has a small
        // independent budget, durable keyset continuation, and isolated bounded provider admission.
        if (scopes is not null)
        {
            using IServiceScope scope = scopes.CreateScope();
            IReminderTargetCatalog? catalog = scope.ServiceProvider.GetService<IReminderTargetCatalog>();
            if (catalog is not null)
            {
                string? after = null;
                ReminderCatalogDiscoveryState? owned = null;
                bool unavailable = false;
                bool complete = false;
                bool budgetYielded = false;
                long discoveryStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                TimeSpan Remaining()
                {
                    TimeSpan remaining = _options.CatalogServiceBudget - System.Diagnostics.Stopwatch.GetElapsedTime(discoveryStarted);
                    if (remaining <= TimeSpan.Zero) { budgetYielded = true; throw new TimeoutException(); }
                    return remaining < _options.CatalogOperationTimeout ? remaining : _options.CatalogOperationTimeout;
                }
                try
                {
                    owned = await ProcessReadDeadline.RunControlAsync(ct => _index.ClaimDiscoveryAsync(false, ct),
                        Remaining(), cancellationToken).ConfigureAwait(false);
                    after = owned.AfterKey;
                    if (after is not null)
                    {
                        string[] parts = after.Split('\0');
                        if (parts.Length != 3) throw new InvalidOperationException("Reminder catalog continuation is invalid.");
                        new ProcessSource(parts[0], parts[1], parts[2]).Validate();
                    }
                    unavailable = owned.IsIncomplete;
                    for (int pageNumber = 0; pageNumber < _options.CatalogPagesPerPass; pageNumber++)
                    {
                        if (System.Diagnostics.Stopwatch.GetElapsedTime(discoveryStarted) >= _options.CatalogServiceBudget) break;
                        ProcessSourcePage page = await ProcessReadDeadline.RunAsync(
                            ct => catalog.ListAsync(after, _options.CatalogPageSize, ct), Remaining(), cancellationToken).ConfigureAwait(false);
                        if (page?.Sources is null || page.Sources.Count > _options.CatalogPageSize)
                            throw new InvalidOperationException("Reminder catalog page is invalid.");
                        string? prior = after;
                        foreach (ProcessSource source in page.Sources)
                        {
                            source.Validate();
                            if (prior is not null && string.CompareOrdinal(source.CatalogKey, prior) <= 0)
                                throw new InvalidOperationException("Reminder catalog keys do not advance.");
                            prior = source.CatalogKey;
                        }
                        if (page.NextKey is not null && (page.Sources.Count == 0 || page.NextKey != prior))
                            throw new InvalidOperationException("Reminder catalog continuation is invalid.");
                        unavailable |= page.IsIncomplete;
                        bool consumedPage = true;
                        foreach (ProcessSource source in page.Sources)
                        {
                            if (System.Diagnostics.Stopwatch.GetElapsedTime(discoveryStarted) >= _options.CatalogServiceBudget)
                            {
                                consumedPage = false;
                                break;
                            }
                            var target = new ReminderTarget(source.Tenant, source.Domain, source.Aggregate);
                            string actor = ReminderCoordinator.ComputeActorId(target);
                            if (!observed.Contains(actor, StringComparer.Ordinal))
                            {
                                try
                                {
                                    // Authoritative convergence publishes only retained work. Terminal or
                                    // no-intent sources cannot fill the index before active sources arrive.
                                    ReminderConvergenceResult result = await ProcessReadDeadline.RunConvergenceAsync(_index.AdmissionScope, actor,
                                        ct => _registrar.ConvergeAsync(target, ct), Remaining(), cancellationToken).ConfigureAwait(false);
                                    observed.Add(actor);
                                    candidates++;
                                    armed += result.Armed;
                                    submitted += result.Submitted;
                                    cancelled += result.Cancelled;
                                    unresolved += result.Unresolved;
                                    quarantined += result.Quarantined;
                                }
                                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                                catch (Exception exception)
                                {
                                    unavailable = true;
                                    ReminderLog.CandidateFailed(_logger, actor, FailureReason(exception), exception.GetType().Name);
                                    // Do not abort later targets or tenants. The next cursor write includes
                                    // this incomplete claim atomically, so no persisted continuation skips it.
                                    // A budget exit uses the same bounded failure-evidence cleanup below.
                                }
                            }
                            after = source.CatalogKey;
                        }
                        bool atEnd = consumedPage && page.NextKey is null;
                        complete = atEnd && !unavailable;
                        string? nextAfter = atEnd ? null : after;
                        owned = await WriteOwnedAsync(false, owned, complete, nextAfter, unavailable,
                            Remaining(), cancellationToken).ConfigureAwait(false);
                        after = nextAfter;
                        if (atEnd || !consumedPage) break;
                    }
                    if (!complete) incomplete++;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception exception)
                {
                    incomplete++;
                    try
                    {
                        // A failed later page must preserve every earlier incomplete/publication claim.
                        TimeSpan cleanup = _options.CatalogOperationTimeout < TimeSpan.FromMilliseconds(500)
                            ? _options.CatalogOperationTimeout : TimeSpan.FromMilliseconds(500);
                        if (owned is not null)
                            await WriteOwnedAsync(false, owned, false, after, unavailable || !budgetYielded,
                                cleanup, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch (Exception) { /* The previous unfinished marker remains authoritative. */ }
                    ReminderLog.ScanFailed(_logger, "target-catalog", "catalog-unavailable", exception.GetType().Name);
                }
            }
        }

        _status.RecordIndexedSlice(observed, indexed, indexedComplete && incomplete == 0);
        _status.CompletePass(_time.GetUtcNow(), incomplete, observed, passVersion, allowPrune: false);
        ReminderLog.PassCompleted(_logger, tenants, candidates, armed, submitted, cancelled, unresolved, quarantined, incomplete);
        return new ReminderReconciliationPass(tenants, candidates, armed, submitted, cancelled, unresolved, quarantined, incomplete, capacityLimitedTenants);
    }

    private Task<ReminderCatalogDiscoveryState> WriteOwnedAsync(bool indexed, ReminderCatalogDiscoveryState owned,
        bool complete, string? after, bool incomplete, TimeSpan timeout, CancellationToken cancellationToken)
        => ProcessReadDeadline.RunControlAsync(ct => _index.UpdateDiscoveryAsync(indexed, owned, complete, after, incomplete, ct),
            timeout, cancellationToken);

    private static string FailureReason(Exception exception)
        => exception is ReminderFailClosedException failure ? failure.ReasonCode : string.Empty;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.ReconciliationEnabled)
        {
            ReminderLog.ReconciliationDisabled(_logger);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            ReminderReconciliationPass pass;
            try
            {
                pass = await RunPassAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            TimeSpan delay = pass.Incomplete > pass.CapacityLimitedTenants && _options.RetryInitialDelay < _options.ReconciliationInterval
                ? _options.RetryInitialDelay
                : _options.ReconciliationInterval;
            try
            {
                await Task.Delay(delay, _time, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
