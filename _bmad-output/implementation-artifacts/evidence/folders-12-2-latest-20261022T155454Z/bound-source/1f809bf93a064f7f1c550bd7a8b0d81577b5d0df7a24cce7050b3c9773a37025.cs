using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Reactivates payload-free write-ahead projection work until it is durably handed off.</summary>
internal sealed class ProjectionActivationWorker(
    IProjectionActivationOutbox outbox,
    IProjectionUpdateOrchestrator projectionOrchestrator,
    IOptions<ProjectionDispatchOptions> options,
    TimeProvider timeProvider,
    IProjectionSourceGuardDelivery? sourceGuardDelivery = null) : BackgroundService {
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        using var timer = new PeriodicTimer(options.Value.RetryWorkerInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false)) {
            await RunOnceAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>Runs one separately scheduled activation/reconciliation pass.</summary>
    internal async Task RunOnceAsync(CancellationToken stoppingToken) {
            IReadOnlyList<ProjectionActivationWorkItem> due;
            try {
                due = await outbox.GetDueAsync(
                    timeProvider.GetUtcNow(),
                    options.Value.RetryScanBatchSize,
                    stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                throw;
            }
            catch {
                return;
            }

            foreach (ProjectionActivationWorkItem workItem in due) {
                CancellationTokenSource? cleanupBudget = null;
                try {
                    var identity = new AggregateIdentity(workItem.TenantId, workItem.Domain, workItem.AggregateId);
                    if (sourceGuardDelivery is not null)
                    {
                        ProjectionSourceGuardDeliveryResult observed = await sourceGuardDelivery.TryDeliverAsync(identity,
                            stoppingToken).ConfigureAwait(false);
                        if (observed.Owned)
                        {
                            cleanupBudget = observed.CreateCleanupBudget(timeProvider);
                            if (observed.AlreadyCurrent && (observed.FinalizedCleanupProven
                                || observed.FinalizationStartedTimestamp is null
                                    && await sourceGuardDelivery.ReclaimCompletedReplayAsync(identity, cleanupBudget.Token).ConfigureAwait(false)))
                            {
                                // CompleteAsync is conditional on this sampled outbox revision;
                                // a later append's activation must survive this cleanup attempt.
                                await outbox.CompleteAsync(workItem, cleanupBudget.Token).ConfigureAwait(false);
                                continue;
                            }
                            await outbox.DeferAsync(workItem,
                                timeProvider.GetUtcNow() + options.Value.RetryBaseDelay, cleanupBudget.Token).ConfigureAwait(false);
                            continue;
                        }
                    }
                    await projectionOrchestrator.UpdateProjectionAsync(
                        identity,
                        stoppingToken).ConfigureAwait(false);
                    await outbox.DeferAsync(
                        workItem,
                        timeProvider.GetUtcNow() + options.Value.RetryBaseDelay,
                        stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                    throw;
                }
                catch {
                    try {
                        cleanupBudget ??= new(TimeSpan.FromSeconds(30), timeProvider);
                        cleanupBudget.Token.ThrowIfCancellationRequested();
                        await outbox.DeferAsync(
                            workItem,
                            timeProvider.GetUtcNow() + options.Value.RetryBaseDelay,
                            cleanupBudget.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                        throw;
                    }
                    catch {
                    }
                }
                finally { cleanupBudget?.Dispose(); }
            }
    }
}
