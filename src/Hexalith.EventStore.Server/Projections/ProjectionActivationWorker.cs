using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Owns bounded, independently leased durable activation attempts for its hosted lifetime.</summary>
internal sealed partial class ProjectionActivationWorker(
    IProjectionActivationOutbox outbox,
    IProjectionUpdateOrchestrator projectionOrchestrator,
    IOptions<ProjectionDispatchOptions> options,
    TimeProvider timeProvider,
    IProjectionSourceGuardDelivery? sourceGuardDelivery = null,
    IRouteCatalogAuthority? catalogAuthority = null,
    ILogger<ProjectionActivationWorker>? logger = null) : BackgroundService
{
    private const int MaximumConcurrentActivations = 8;
    private readonly Dictionary<string, Task> _running = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _scanGate = new(1, 1);
    private readonly HashSet<string> _deferFailures = new(StringComparer.Ordinal);
    private int _isRunning;
    private bool _scanFailed;
    private ILogger<ProjectionActivationWorker> Logger => logger ?? NullLogger<ProjectionActivationWorker>.Instance;

    /// <summary>Gets whether the actual hosted lifetime is accepting work.</summary>
    internal bool IsRunning => Volatile.Read(ref _isRunning) != 0;

    /// <summary>Checks the precise outbox used by this worker.</summary>
    internal bool OwnsOutbox(IProjectionActivationOutbox candidate) => ReferenceEquals(outbox, candidate);

    /// <inheritdoc/>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Volatile.Write(ref _isRunning, 1);
        try { await base.StartAsync(cancellationToken).ConfigureAwait(false); }
        catch { Volatile.Write(ref _isRunning, 0); throw; }
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        Volatile.Write(ref _isRunning, 0);
        // Honor the host's wait deadline. ExecuteAsync keeps ownership of its slots
        // and independently drains actual calls even after this wait is canceled.
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        Volatile.Write(ref _isRunning, 0);
        base.Dispose();
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(options.Value.RetryWorkerInterval, timeProvider);
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                _ = await ScheduleOnceAsync(stoppingToken).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _isRunning, 0);
            // Calls which ignore cancellation still hold their slot and catalog lease.
            await ObserveAsync(_running.Values.ToArray(), stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>Runs and awaits one pass for isolated protocol verification.</summary>
    internal async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        await ObserveAsync(await ScheduleOnceAsync(stoppingToken).ConfigureAwait(false), stoppingToken).ConfigureAwait(false);
        stoppingToken.ThrowIfCancellationRequested();
    }

    private async Task<IReadOnlyList<Task>> ScheduleOnceAsync(CancellationToken stoppingToken)
    {
        await _scanGate.WaitAsync(stoppingToken).ConfigureAwait(false);
        try
        {
            foreach (string key in _running.Where(static pair => pair.Value.IsCompleted).Select(static pair => pair.Key).ToArray())
            {
                await ObserveAsync([_running[key]], stoppingToken).ConfigureAwait(false);
                _running.Remove(key);
            }
            IReadOnlyList<ProjectionActivationWorkItem> due;
            try
            {
                // Active IDs can occupy the oldest due rows. Include a bounded allowance
                // for them without increasing the configured number of new pending attempts.
                int scanCount = (int)Math.Min(int.MaxValue, (long)options.Value.RetryScanBatchSize + _running.Count);
                due = await outbox.GetDueAsync(timeProvider.GetUtcNow(), scanCount, stoppingToken).ConfigureAwait(false);
                if (_scanFailed) ProjectionActivationWorkerLog.Recovered(Logger, "scan");
                _scanFailed = false;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
            catch
            {
                _scanFailed = true;
                ProjectionActivationWorkerLog.Failed(Logger, "scan", "activation_scan_failed");
                return [];
            }

            var started = new List<Task>();
            foreach (ProjectionActivationWorkItem workItem in due)
            {
                if (_running.Count >= MaximumConcurrentActivations || started.Count >= options.Value.RetryScanBatchSize) break;
                if (_running.ContainsKey(workItem.ActivationId)) continue;
                Task attempt = DeliverAsync(workItem, stoppingToken);
                _running.Add(workItem.ActivationId, attempt);
                started.Add(attempt);
            }
            return started;
        }
        finally { _scanGate.Release(); }
    }

    private static async Task ObserveAsync(IEnumerable<Task> tasks, CancellationToken stoppingToken)
    {
        try { await Task.WhenAll(tasks).ConfigureAwait(false); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task DeliverAsync(ProjectionActivationWorkItem workItem, CancellationToken stoppingToken)
    {
        try { await DeliverLeasedAsync(workItem, stoppingToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
        catch { ProjectionActivationWorkerLog.Failed(Logger, "delivery", "activation_authority_failed"); }
    }

    private async Task DeliverLeasedAsync(ProjectionActivationWorkItem workItem, CancellationToken stoppingToken)
    {
        // One independently acquired authority spans source, every committed method, state and
        // conditional cleanup. Never inherit a completed HTTP request's dispatch scope.
        RouteCatalogDispatchLease? lease = catalogAuthority is null ? null
            : await catalogAuthority.AcquireAsync(stoppingToken).ConfigureAwait(false);
        await using var release = new RouteCatalogLeaseReleaser(lease, stoppingToken).ConfigureAwait(false);
        CancellationTokenSource? cleanupBudget = null;
        try
        {
            var identity = new AggregateIdentity(workItem.TenantId, workItem.Domain, workItem.AggregateId);
            ProjectionSourceGuardDeliveryResult? observed;
            if (projectionOrchestrator is ProjectionUpdateOrchestrator production)
                observed = await production.DeliverActivationAsync(identity, lease?.Snapshot, stoppingToken).ConfigureAwait(false);
            else
            {
                observed = sourceGuardDelivery is null ? null
                    : await sourceGuardDelivery.TryDeliverAsync(identity, stoppingToken).ConfigureAwait(false);
                if (observed?.Owned != true)
                    await projectionOrchestrator.UpdateProjectionAsync(identity, stoppingToken).ConfigureAwait(false);
            }
            if (observed?.Owned == true)
            {
                cleanupBudget = observed.CreateCleanupBudget(timeProvider);
                if (observed.AlreadyCurrent && (observed.FinalizedCleanupProven
                    || observed.FinalizationStartedTimestamp is null
                        && await sourceGuardDelivery!.ReclaimCompletedReplayAsync(identity, cleanupBudget.Token).ConfigureAwait(false)))
                {
                    await outbox.CompleteAsync(workItem, cleanupBudget.Token).ConfigureAwait(false);
                    RecoverCompletion(workItem);
                    return;
                }
                ProjectionActivationWorkerLog.GuardRefused(Logger, observed.ReasonCode);
            }
            if (observed?.Owned != true)
            {
                ProjectionActivationWorkItem? remaining = await outbox.GetAsync(identity, stoppingToken).ConfigureAwait(false);
                if (remaining is null)
                {
                    RecoverCompletion(workItem);
                    return;
                }
                ProjectionActivationWorkerLog.Failed(Logger, "delivery", "activation_delivery_pending");
            }
            DateTimeOffset nextDue = timeProvider.GetUtcNow() + options.Value.RetryBaseDelay;
            CancellationToken deferToken = cleanupBudget?.Token ?? stoppingToken;
            await outbox.DeferAsync(workItem, nextDue, deferToken).ConfigureAwait(false);
            await ConfirmDeferRecoveryAsync(workItem, nextDue, deferToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
        catch
        {
            ProjectionActivationWorkerLog.Failed(Logger, "delivery", "activation_delivery_failed");
            try
            {
                cleanupBudget ??= new(TimeSpan.FromSeconds(30), timeProvider);
                cleanupBudget.Token.ThrowIfCancellationRequested();
                DateTimeOffset nextDue = timeProvider.GetUtcNow() + options.Value.RetryBaseDelay;
                await outbox.DeferAsync(workItem, nextDue, cleanupBudget.Token).ConfigureAwait(false);
                await ConfirmDeferRecoveryAsync(workItem, nextDue, cleanupBudget.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
            catch
            {
                lock (_deferFailures)
                    if (_deferFailures.Count < 64) _deferFailures.Add(workItem.ActivationId);
                ProjectionActivationWorkerLog.Failed(Logger, "defer", "activation_defer_failed");
            }
        }
        finally
        {
            cleanupBudget?.Dispose();
            // A canceled protocol wait can finish before an enrolled native RPC. Keep
            // this hosted slot and its exact authority until that actual call ends.
            // Draining never clears Retain() or converts an ambiguous outcome to proof.
            if (lease is not null) await lease.DrainRemoteCallsAsync().ConfigureAwait(false);
        }
    }

    private async Task ConfirmDeferRecoveryAsync(ProjectionActivationWorkItem workItem, DateTimeOffset nextDue, CancellationToken token)
    {
        lock (_deferFailures)
            if (!_deferFailures.Contains(workItem.ActivationId)) return;
        try
        {
            var identity = new AggregateIdentity(workItem.TenantId, workItem.Domain, workItem.AggregateId);
            ProjectionActivationWorkItem? persisted = await outbox.GetAsync(identity, token).ConfigureAwait(false);
            // A conditional no-op against a newer revision is not demonstrated persistence.
            if (persisted is null || persisted.ActivationId != workItem.ActivationId
                || persisted.TenantId != workItem.TenantId || persisted.Domain != workItem.Domain || persisted.AggregateId != workItem.AggregateId
                || persisted.Revision != workItem.Revision || (long)persisted.Attempt != (long)workItem.Attempt + 1
                || persisted.NextDueUtc != nextDue) return;
            bool recovered;
            lock (_deferFailures) recovered = _deferFailures.Remove(workItem.ActivationId);
            if (recovered) ProjectionActivationWorkerLog.Recovered(Logger, "defer");
        }
        catch
        {
            // This read is advisory diagnostics. Its failure cannot change the durable
            // deferral result or turn a successful conditional write into another attempt.
        }
    }

    private void RecoverCompletion(ProjectionActivationWorkItem workItem)
    {
        bool deferredFailure;
        lock (_deferFailures) deferredFailure = _deferFailures.Remove(workItem.ActivationId);
        if (workItem.Attempt > 0 || deferredFailure) ProjectionActivationWorkerLog.Recovered(Logger, "delivery");
    }
}
