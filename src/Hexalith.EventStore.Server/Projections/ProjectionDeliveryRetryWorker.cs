using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Hosted worker that reloads bounded aggregate history for durable immediate-mode retry work.</summary>
internal sealed partial class ProjectionDeliveryRetryWorker(
    IProjectionDeliveryRetryScheduler scheduler,
    INamedProjectionDispatchCoordinator coordinator,
    IActorProxyFactory actorProxyFactory,
    IEventPayloadProtectionService payloadProtectionService,
    IOptions<EventStoreActorOptions> actorOptions,
    IOptions<ProjectionDispatchOptions> dispatchOptions,
    IProjectionRebuildCheckpointStore rebuildCheckpointStore,
    TimeProvider timeProvider,
    ILogger<ProjectionDeliveryRetryWorker> logger) : BackgroundService {
    private const int ReadPageSize = 256;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        using var timer = new PeriodicTimer(dispatchOptions.Value.RetryWorkerInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false)) {
            try {
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                throw;
            }
            catch (Exception) {
                // A transient state-store/DAPR fault (including ledger optimistic-concurrency
                // exhaustion) must never fault this BackgroundService: the .NET default
                // BackgroundServiceExceptionBehavior.StopHost would otherwise terminate the host.
                // Log and continue; due work is retried on the next tick.
                Log.RetryActivationFailed(logger, ProjectionDispatchReasonCodes.DeliveryStateUnavailable);
            }
        }
    }

    /// <summary>Runs one bounded retry activation.</summary>
    /// <param name="cancellationToken">Propagates worker shutdown.</param>
    internal async Task RunOnceAsync(CancellationToken cancellationToken) {
        ProjectionDispatchOptions options = dispatchOptions.Value;
        options.Validate();
        IReadOnlyList<ProjectionDeliveryRetryWorkItem> due = await scheduler
            .GetDueAsync(timeProvider.GetUtcNow(), options.RetryScanBatchSize, cancellationToken)
            .ConfigureAwait(false);
        foreach (ProjectionDeliveryRetryWorkItem workItem in due) {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessAsync(workItem, options, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessAsync(
        ProjectionDeliveryRetryWorkItem workItem,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken) {
        var identity = new AggregateIdentity(workItem.TenantId, workItem.Domain, workItem.AggregateId);
        using IDisposable projectionLock = await ProjectionUpdateOrchestrator.ProjectionLocks
            .AcquireAsync(identity.ActorId, cancellationToken)
            .ConfigureAwait(false);
        try {
            if (await rebuildCheckpointStore
                .HasActiveOperatorRebuildForDomainAsync(
                    identity.TenantId,
                    identity.Domain,
                    cancellationToken)
                .ConfigureAwait(false)) {
                await DeferAsync(workItem, options, cancellationToken).ConfigureAwait(false);
                return;
            }

            EventEnvelope[] events = await LoadHistoryAsync(identity, workItem, cancellationToken).ConfigureAwait(false);
            if (events.Length == 0
                || events[^1].SequenceNumber != workItem.HeadSequence
                || !string.Equals(events[^1].MessageId, workItem.HeadMessageId, StringComparison.Ordinal)) {
                await DeferAsync(workItem, options, cancellationToken).ConfigureAwait(false);
                return;
            }

            ProjectionEventReadabilityResult readability = await ProjectionEventWireBuilder
                .BuildAsync(payloadProtectionService, identity, events, cancellationToken)
                .ConfigureAwait(false);
            if (readability.Events is null) {
                await DeferAsync(workItem, options, cancellationToken).ConfigureAwait(false);
                return;
            }

            var registration = new DomainServiceRegistration(
                workItem.AppId,
                "project/v2",
                workItem.TenantId,
                workItem.Domain,
                workItem.ServiceVersion);
            bool handled = await coordinator
                .TryDispatchAsync(identity, registration, events, readability.Events, cancellationToken)
                .ConfigureAwait(false);
            if (!handled) {
                await DeferAsync(workItem, options, cancellationToken).ConfigureAwait(false);
                return;
            }
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception) {
            Log.RetryFailed(logger, ProjectionDispatchReasonCodes.PartialRetry);
            try {
                await DeferAsync(workItem, options, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch {
            }
        }
    }

    private async Task<EventEnvelope[]> LoadHistoryAsync(
        AggregateIdentity identity,
        ProjectionDeliveryRetryWorkItem workItem,
        CancellationToken cancellationToken) {
        IAggregateActor aggregateProxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(identity.ActorId),
            actorOptions.Value.AggregateActorTypeName);
        var events = new List<EventEnvelope>();
        long afterSequence = 0;
        while (afterSequence < workItem.HeadSequence) {
            cancellationToken.ThrowIfCancellationRequested();
            EventEnvelope[] page = await aggregateProxy
                .ReadEventsRangeAsync(afterSequence, workItem.HeadSequence, ReadPageSize)
                .ConfigureAwait(false);
            if (page.Length == 0) {
                break;
            }

            EventEnvelope[] ordered = [.. page
                .Where(item => item.SequenceNumber > afterSequence && item.SequenceNumber <= workItem.HeadSequence)
                .OrderBy(static item => item.SequenceNumber)];
            if (ordered.Length == 0) {
                break;
            }

            events.AddRange(ordered);
            long nextSequence = ordered[^1].SequenceNumber;
            if (nextSequence <= afterSequence) {
                break;
            }

            afterSequence = nextSequence;
        }

        return [.. events];
    }

    private async Task DeferAsync(
        ProjectionDeliveryRetryWorkItem workItem,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken) {
        ProjectionDeliveryRetryWorkItem? claimed = await scheduler.TryAcquireAsync(
            workItem,
            Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture),
            timeProvider.GetUtcNow(),
            options.RetryLeaseDuration,
            cancellationToken).ConfigureAwait(false);
        if (claimed is null) {
            return;
        }

        int attempt = Math.Min(claimed.Attempt + 1, options.MaxRetryAttempts);
        double multiplier = Math.Pow(2, Math.Max(0, attempt - 1));
        long delayTicks = (long)Math.Min(options.RetryMaxDelay.Ticks, options.RetryBaseDelay.Ticks * multiplier);
        _ = await scheduler.TryUpdateAsync(
            claimed with {
                Attempt = attempt,
                NextDueUtc = timeProvider.GetUtcNow() + TimeSpan.FromTicks(delayTicks),
                LastReasonCode = ProjectionDispatchReasonCodes.PartialRetry,
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 4660,
            Level = LogLevel.Warning,
            Message = "Named projection retry remains pending: ReasonCode={ReasonCode}, Stage=NamedProjectionRetry")]
        public static partial void RetryFailed(ILogger logger, string reasonCode);

        [LoggerMessage(
            EventId = 4661,
            Level = LogLevel.Error,
            Message = "Named projection retry activation failed; the worker continues on the next tick. ReasonCode={ReasonCode}, Stage=NamedProjectionRetryActivation")]
        public static partial void RetryActivationFailed(ILogger logger, string reasonCode);
    }
}
