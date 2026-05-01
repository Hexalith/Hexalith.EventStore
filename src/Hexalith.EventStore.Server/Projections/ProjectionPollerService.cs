using System.Collections.Concurrent;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Background worker that delivers tracked projection updates for domains configured with polling intervals.
/// </summary>
public sealed partial class ProjectionPollerService(
    IProjectionCheckpointTracker checkpointTracker,
    IProjectionUpdateOrchestrator projectionOrchestrator,
    IOptions<ProjectionOptions> projectionOptions,
    IProjectionPollerTickSource tickSource,
    TimeProvider timeProvider,
    ILogger<ProjectionPollerService> logger) : BackgroundService {
    private const int MaxIdentitiesPerTick = 100;
    private readonly ConcurrentDictionary<string, byte> _activeIdentities = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _nextDueByDomain = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Runs one polling pass. Exposed internally for deterministic tests.
    /// </summary>
    /// <param name="now">The current timestamp.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal async Task PollOnceAsync(DateTimeOffset now, CancellationToken cancellationToken = default) {
        ProjectionOptions options = projectionOptions.Value;
        HashSet<string> dueDomains = new(StringComparer.OrdinalIgnoreCase);
        int processed = 0;

        await foreach (AggregateIdentity identity in checkpointTracker.EnumerateTrackedIdentitiesAsync(cancellationToken).ConfigureAwait(false)) {
            int refreshIntervalMs = options.GetRefreshIntervalMs(identity.Domain);
            if (refreshIntervalMs <= 0 || !IsDomainDue(identity.Domain, refreshIntervalMs, now, dueDomains)) {
                continue;
            }

            if (processed >= MaxIdentitiesPerTick) {
                Log.TickLimitReached(logger, MaxIdentitiesPerTick);
                break;
            }

            if (!_activeIdentities.TryAdd(identity.ActorId, 0)) {
                Log.IdentityAlreadyRunning(logger, identity.TenantId, identity.Domain, identity.AggregateId);
                continue;
            }

            processed++;
            try {
                Log.IdentityDeliveryStarted(logger, identity.TenantId, identity.Domain, identity.AggregateId, refreshIntervalMs);
                await projectionOrchestrator.DeliverProjectionAsync(identity, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) {
                Log.IdentityDeliveryFailed(logger, ex, identity.TenantId, identity.Domain, identity.AggregateId, ex.GetType().Name);
            }
            finally {
                _ = _activeIdentities.TryRemove(identity.ActorId, out _);
            }
        }

        foreach (string domain in dueDomains) {
            int intervalMs = options.GetRefreshIntervalMs(domain);
            if (intervalMs > 0) {
                _nextDueByDomain[domain] = now.AddMilliseconds(intervalMs);
            }
        }
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            TimeSpan interval = GetSmallestPositiveInterval(projectionOptions.Value);
            bool ticked = await tickSource.WaitForNextTickAsync(interval, stoppingToken).ConfigureAwait(false);
            if (!ticked) {
                return;
            }

            await PollOnceAsync(timeProvider.GetUtcNow(), stoppingToken).ConfigureAwait(false);
        }
    }

    private bool IsDomainDue(string domain, int refreshIntervalMs, DateTimeOffset now, HashSet<string> dueDomains) {
        if (!_nextDueByDomain.TryGetValue(domain, out DateTimeOffset nextDue)) {
            _nextDueByDomain[domain] = now;
            nextDue = now;
        }

        if (now < nextDue) {
            return false;
        }

        _ = dueDomains.Add(domain);
        return true;
    }

    private static TimeSpan GetSmallestPositiveInterval(ProjectionOptions options) {
        int intervalMs = options.DefaultRefreshIntervalMs > 0 ? options.DefaultRefreshIntervalMs : int.MaxValue;
        foreach (DomainProjectionOptions domainOptions in options.Domains.Values) {
            if (domainOptions.RefreshIntervalMs > 0) {
                intervalMs = Math.Min(intervalMs, domainOptions.RefreshIntervalMs);
            }
        }

        return TimeSpan.FromMilliseconds(intervalMs == int.MaxValue ? 60_000 : intervalMs);
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 1130,
            Level = LogLevel.Debug,
            Message = "Projection poller delivery started: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, RefreshIntervalMs={RefreshIntervalMs}, Stage=ProjectionPollingDeliveryStarted")]
        public static partial void IdentityDeliveryStarted(ILogger logger, string tenantId, string domain, string aggregateId, int refreshIntervalMs);

        [LoggerMessage(
            EventId = 1131,
            Level = LogLevel.Warning,
            Message = "Projection poller delivery failed: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ExceptionType={ExceptionType}, Stage=ProjectionPollingDeliveryFailed")]
        public static partial void IdentityDeliveryFailed(ILogger logger, Exception ex, string tenantId, string domain, string aggregateId, string exceptionType);

        [LoggerMessage(
            EventId = 1132,
            Level = LogLevel.Debug,
            Message = "Projection poller skipped identity already running in this process: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, Stage=ProjectionPollingAlreadyRunning")]
        public static partial void IdentityAlreadyRunning(ILogger logger, string tenantId, string domain, string aggregateId);

        [LoggerMessage(
            EventId = 1133,
            Level = LogLevel.Information,
            Message = "Projection poller reached per-tick identity limit: Limit={Limit}, Stage=ProjectionPollingTickLimitReached")]
        public static partial void TickLimitReached(ILogger logger, int limit);
    }
}

public interface IProjectionPollerTickSource {
    Task<bool> WaitForNextTickAsync(TimeSpan interval, CancellationToken cancellationToken);
}

public sealed class PeriodicProjectionPollerTickSource : IProjectionPollerTickSource {
    public async Task<bool> WaitForNextTickAsync(TimeSpan interval, CancellationToken cancellationToken) {
        using var timer = new PeriodicTimer(interval);
        return await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
    }
}
