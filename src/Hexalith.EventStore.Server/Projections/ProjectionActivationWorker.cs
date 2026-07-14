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
    TimeProvider timeProvider) : BackgroundService {
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        using var timer = new PeriodicTimer(options.Value.RetryWorkerInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false)) {
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
                continue;
            }

            foreach (ProjectionActivationWorkItem workItem in due) {
                try {
                    await projectionOrchestrator.UpdateProjectionAsync(
                        new AggregateIdentity(workItem.TenantId, workItem.Domain, workItem.AggregateId),
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
                        await outbox.DeferAsync(
                            workItem,
                            timeProvider.GetUtcNow() + options.Value.RetryBaseDelay,
                            stoppingToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                        throw;
                    }
                    catch {
                    }
                }
            }
        }
    }
}
