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
    IProjectionPollerDeliveryGateway projectionDeliveryGateway,
    IOptions<ProjectionOptions> projectionOptions,
    IProjectionPollerTickSource tickSource,
    TimeProvider timeProvider,
    ILogger<ProjectionPollerService> logger) : BackgroundService {
    private const int MaxIdentitiesPerTick = 100;
    private readonly ConcurrentDictionary<string, byte> _activeIdentities = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _nextDueByDomain = new(StringComparer.OrdinalIgnoreCase);

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
        bool tickLimited = false;
        bool enumerationFailed = false;

        // R2P1 — when the per-tick cap fires we must skip schedule advancement only for the
        // domain whose enumeration was interrupted. Earlier domains in the enumeration order
        // are fully covered (the underlying tracker iterates one scope at a time) and must be
        // advanced; the prior implementation skipped advancement for ALL domains, permanently
        // starving alphabetically-later domains when a single domain's identity count exceeded
        // the cap.
        string? truncatedDomain = null;

        try {
            await foreach (AggregateIdentity identity in checkpointTracker.EnumerateTrackedIdentitiesAsync(cancellationToken).ConfigureAwait(false)) {
                int refreshIntervalMs = options.GetRefreshIntervalMs(identity.Domain);
                if (refreshIntervalMs <= 0 || !IsDomainDue(identity.Domain, refreshIntervalMs, now, dueDomains)) {
                    continue;
                }

                if (processed >= MaxIdentitiesPerTick) {
                    Log.TickLimitReached(logger, MaxIdentitiesPerTick);
                    tickLimited = true;
                    truncatedDomain = identity.Domain;
                    break;
                }

                if (!_activeIdentities.TryAdd(identity.ActorId, 0)) {
                    Log.IdentityAlreadyRunning(logger, identity.TenantId, identity.Domain, identity.AggregateId);
                    continue;
                }

                processed++;
                try {
                    Log.IdentityDeliveryStarted(logger, identity.TenantId, identity.Domain, identity.AggregateId, refreshIntervalMs);
                    await projectionDeliveryGateway.DeliverProjectionAsync(identity, cancellationToken).ConfigureAwait(false);
                }
                // R2P4 — only rethrow OperationCanceledException when the host's stopping token is the
                // source. A linked-CTS timeout (e.g. an inner HTTP timeout) raised inside DeliverProjectionAsync
                // must be treated as a transient delivery failure for that identity, not as a tick-wide abort.
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                    throw;
                }
                catch (Exception ex) {
                    Log.IdentityDeliveryFailed(logger, ex, identity.TenantId, identity.Domain, identity.AggregateId, ex.GetType().Name);
                }
                finally {
                    _ = _activeIdentities.TryRemove(identity.ActorId, out _);
                }
            }
        }
        // R2P4 — same OCE-source filter on the outer rethrow.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch (Exception ex) {
            enumerationFailed = true;
            Log.EnumerationFailed(logger, ex, ex.GetType().Name);
        }

        // R2P2 — when enumeration failed before any identity was yielded, dueDomains is empty and
        // the post-loop foreach is a no-op, leaving _nextDueByDomain at its seed (now). IsDomainDue
        // would then return true on every smallest-interval tick, producing a retry storm that hammers
        // the failing tracker. Advance every already-known polling domain by its configured interval
        // so the next attempt happens on a normal tick boundary.
        if (enumerationFailed) {
            AdvanceKnownPollingDomains(options, now);
            return;
        }

        foreach (string domain in dueDomains) {
            int intervalMs = options.GetRefreshIntervalMs(domain);
            if (intervalMs <= 0) {
                continue;
            }

            // R2P1 — skip the truncated domain only; advance fully-covered earlier domains.
            if (tickLimited && string.Equals(domain, truncatedDomain, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            // R2P7 — atomic write that preserves the freshest scheduled time under concurrent ticks.
            // The previous indexer-set (`_nextDueByDomain[domain] = ...`) was last-writer-wins, so a
            // slow concurrent tick could overwrite a fresher schedule produced by a later tick.
            ScheduleNextDue(domain, now.AddMilliseconds(intervalMs));
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

    private void AdvanceKnownPollingDomains(ProjectionOptions options, DateTimeOffset now) {
        // Snapshot the keys to avoid concurrent-modification surprises if a parallel tick mutates
        // the dictionary mid-iteration. A small allocation per failed tick is cheaper than the
        // retry storm this advancement prevents.
        foreach (string domain in _nextDueByDomain.Keys.ToArray()) {
            int intervalMs = options.GetRefreshIntervalMs(domain);
            if (intervalMs <= 0) {
                continue;
            }

            ScheduleNextDue(domain, now.AddMilliseconds(intervalMs));
        }
    }

    private void ScheduleNextDue(string domain, DateTimeOffset candidate) =>
        _ = _nextDueByDomain.AddOrUpdate(
            domain,
            candidate,
            (_, existing) => existing >= candidate ? existing : candidate);

    private bool IsDomainDue(string domain, int refreshIntervalMs, DateTimeOffset now, HashSet<string> dueDomains) {
        DateTimeOffset nextDue = _nextDueByDomain.GetOrAdd(domain, now);

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

        [LoggerMessage(
            EventId = 1134,
            Level = LogLevel.Warning,
            Message = "Projection poller enumeration failed: ExceptionType={ExceptionType}, Stage=ProjectionPollingEnumerationFailed")]
        public static partial void EnumerationFailed(ILogger logger, Exception ex, string exceptionType);
    }
}

/// <summary>
/// Abstracts the timing source used by <see cref="ProjectionPollerService"/> so tests can drive ticks deterministically.
/// </summary>
/// <remarks>
/// Implementations MUST treat a <see langword="false"/> return as "host shutdown requested via the supplied
/// <paramref name="cancellationToken"/>". Any other failure (timer disposed, internal exception, etc.) MUST throw
/// rather than return <see langword="false"/>; <see cref="ProjectionPollerService.ExecuteAsync"/> exits the
/// background loop on <see langword="false"/> and a silent <see langword="false"/> from a non-cancellation source
/// would shut the poller down without an operator signal.
/// </remarks>
public interface IProjectionPollerTickSource {
    /// <summary>
    /// Waits for the next polling tick.
    /// </summary>
    /// <param name="interval">The polling interval.</param>
    /// <param name="cancellationToken">Cancellation token. Must signal host shutdown for a <see langword="false"/> return to be valid.</param>
    /// <returns><see langword="true"/> when the interval elapsed naturally; <see langword="false"/> when the supplied <paramref name="cancellationToken"/> was cancelled.</returns>
    Task<bool> WaitForNextTickAsync(TimeSpan interval, CancellationToken cancellationToken);
}

/// <summary>
/// Default <see cref="IProjectionPollerTickSource"/> backed by <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.
/// </summary>
/// <remarks>
/// <para>
/// Each call awaits the requested interval, then returns <see langword="true"/>; cancellation of the supplied
/// token returns <see langword="false"/>. A <see cref="CancellationToken.None"/> token cannot cancel, so the call
/// always waits the full interval and returns <see langword="true"/>.
/// </para>
/// <para>
/// The cadence is recomputed by <see cref="ProjectionPollerService.ExecuteAsync"/> on every iteration so the poller
/// adapts to live <see cref="ProjectionOptions"/> changes on the next tick boundary; this means polling drifts by
/// the duration of the prior poll pass. Drift is acceptable under the at-least-once polling contract.
/// </para>
/// </remarks>
public sealed partial class PeriodicProjectionPollerTickSource(ILogger<PeriodicProjectionPollerTickSource>? logger = null) : IProjectionPollerTickSource {
    private static readonly TimeSpan MaxInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan FallbackInterval = TimeSpan.FromSeconds(60);
    private int _clampWarningEmitted;

    /// <inheritdoc/>
    public async Task<bool> WaitForNextTickAsync(TimeSpan interval, CancellationToken cancellationToken) {
        if (interval <= TimeSpan.Zero) {
            // R2P8 — log once when clamping a non-positive interval so misconfig is observable rather
            // than silently coerced to the fallback. Use Interlocked so concurrent first-call paths still
            // emit at most one warning.
            if (logger is not null && Interlocked.Exchange(ref _clampWarningEmitted, 1) == 0) {
                Log.IntervalClampedNonPositive(logger, interval.TotalMilliseconds, FallbackInterval.TotalMilliseconds);
            }

            interval = FallbackInterval;
        }
        else if (interval > MaxInterval) {
            // R2P12 — Task.Delay throws ArgumentOutOfRangeException for intervals beyond uint.MaxValue-1 ms
            // (~49 days). Clamp to 24 h, far past any sane polling cadence.
            if (logger is not null) {
                Log.IntervalClampedTooLarge(logger, interval.TotalHours, MaxInterval.TotalHours);
            }

            interval = MaxInterval;
        }

        try {
            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            return true;
        }
        // R2P3 — only treat the supplied cancellation token's cancellation as the "stop ticking" signal.
        // Any other OCE source (e.g. an ambient AsyncLocal cancellation) propagates so ExecuteAsync sees
        // it as an unexpected error rather than a graceful shutdown.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            return false;
        }
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 1135,
            Level = LogLevel.Warning,
            Message = "Projection poller interval clamped to fallback because the configured interval was non-positive: ConfiguredMs={ConfiguredMs}, FallbackMs={FallbackMs}, Stage=ProjectionPollingIntervalClampedNonPositive")]
        public static partial void IntervalClampedNonPositive(ILogger logger, double configuredMs, double fallbackMs);

        [LoggerMessage(
            EventId = 1136,
            Level = LogLevel.Warning,
            Message = "Projection poller interval clamped to upper bound because the configured interval would overflow Task.Delay: ConfiguredHours={ConfiguredHours}, MaxHours={MaxHours}, Stage=ProjectionPollingIntervalClampedTooLarge")]
        public static partial void IntervalClampedTooLarge(ILogger logger, double configuredHours, double maxHours);
    }
}
