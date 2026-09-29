using Hexalith.EventStore.Client.Reminders;
using Hexalith.EventStore.Contracts.Reminders;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Periodic reminder reconciliation. The first pass starts with the host; later passes follow
/// <see cref="EventStoreReminderOptions.ReconciliationInterval"/>, or the shorter retry delay after an
/// incomplete pass. Each pass walks the discovery index and converges every candidate from its stream, so a
/// lost firing is reissued when due and a deleted scheduler reminder is re-armed when still in the future.
/// </summary>
internal sealed class ReminderReconciler(
    ReminderIntentIndex index,
    IReminderRegistrar registrar,
    ReminderRuntimeStatus status,
    IOptions<EventStoreReminderOptions> options,
    TimeProvider timeProvider,
    ILogger<ReminderReconciler> logger) : BackgroundService
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
        var observed = new List<string>();
        int tenants = 0;
        int candidates = 0;
        int armed = 0;
        int submitted = 0;
        int cancelled = 0;
        int unresolved = 0;
        int quarantined = 0;
        int incomplete = 0;

        IReadOnlyList<string> tenantList;
        try
        {
            tenantList = await _index.ListTenantsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            ReminderLog.ScanFailed(_logger, "tenant-registry", exception.GetType().Name);
            _status.CompletePass(_time.GetUtcNow(), 1, observed);
            return new ReminderReconciliationPass(0, 0, 0, 0, 0, 0, 0, 1);
        }

        foreach (string tenant in tenantList)
        {
            tenants++;
            IReadOnlyList<ReminderCandidate> tenantCandidates;
            try
            {
                tenantCandidates = await _index.ListCandidatesAsync(tenant, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // One unreadable tenant index never blocks the others; the pass stays incomplete.
                ReminderLog.ScanFailed(_logger, "tenant-candidates", exception.GetType().Name);
                incomplete++;
                continue;
            }

            foreach (ReminderCandidate candidate in tenantCandidates)
            {
                candidates++;
                observed.Add(candidate.ActorId);
                try
                {
                    ReminderConvergenceResult result = await _registrar
                        .ConvergeAsync(new ReminderTarget(tenant, candidate.Domain, candidate.Aggregate), cancellationToken)
                        .ConfigureAwait(false);
                    armed += result.Armed;
                    submitted += result.Submitted;
                    cancelled += result.Cancelled;
                    unresolved += result.Unresolved;
                    quarantined += result.Quarantined;
                }
                catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    ReminderLog.CandidateFailed(_logger, candidate.ActorId, exception.GetType().Name);
                    incomplete++;
                }
            }
        }

        _status.CompletePass(_time.GetUtcNow(), incomplete, observed);
        ReminderLog.PassCompleted(_logger, tenants, candidates, armed, submitted, cancelled, unresolved, quarantined, incomplete);
        return new ReminderReconciliationPass(tenants, candidates, armed, submitted, cancelled, unresolved, quarantined, incomplete);
    }

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

            TimeSpan delay = pass.Incomplete > 0 && _options.RetryInitialDelay < _options.ReconciliationInterval
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
