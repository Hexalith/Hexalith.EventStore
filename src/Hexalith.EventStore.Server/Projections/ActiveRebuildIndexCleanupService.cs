using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// P-DEC1-8P (pass-8): hosted background service that periodically clears orphan entries from
/// the projection-rebuild active-index. Recovers from partial best-effort active-index writes
/// left behind by the P13-6P terminal-write recovery path in
/// <see cref="ProjectionRebuildCheckpointStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Cadence is controlled by <see cref="ProjectionOptions.RebuildIndexCleanupCadenceSeconds"/>.
/// Default is 60 seconds. Set to 0 to disable the service entirely.
/// </para>
/// <para>
/// Each tick: enumerates known (tenant, domain) pairs via the projection checkpoint tracker, then
/// for each pair calls <see cref="IProjectionRebuildCheckpointStore.ClearOrphanActiveRebuildIndexEntriesAsync"/>
/// which reads the active-index, probes each projectionName's operator-scope checkpoint, and
/// removes entries whose checkpoint is terminal (Succeeded/Failed/Canceled) or missing.
/// </para>
/// <para>
/// Failures are logged at Warning level and do not interrupt the service loop. The service
/// continues running until the host signals stop.
/// </para>
/// </remarks>
public sealed partial class ActiveRebuildIndexCleanupService(
    IServiceProvider services,
    IOptions<ProjectionOptions> options,
    ILogger<ActiveRebuildIndexCleanupService> logger) : BackgroundService {
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        int cadenceSeconds = options.Value.RebuildIndexCleanupCadenceSeconds;
        if (cadenceSeconds <= 0) {
            Log.CleanupServiceDisabled(logger);
            return;
        }

        var cadence = TimeSpan.FromSeconds(cadenceSeconds);
        Log.CleanupServiceStarted(logger, cadenceSeconds);

        // Initial delay so the service does not race startup of other components.
        try {
            await Task.Delay(cadence, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
            return;
        }

        while (!stoppingToken.IsCancellationRequested) {
            try {
                await RunSweepAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException) {
                Log.CleanupSweepFailed(logger, ex, ex.GetType().Name);
            }

            try {
                await Task.Delay(cadence, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                break;
            }
        }
    }

    private async Task RunSweepAsync(CancellationToken cancellationToken) {
        // Resolve scope per tick so transient dependencies (DaprClient typically singleton, but
        // the checkpoint tracker may carry per-request state) are correctly scoped.
        AsyncServiceScope scope = services.CreateAsyncScope();
        try {
            IProjectionRebuildCheckpointStore checkpointStore = scope.ServiceProvider
                .GetRequiredService<IProjectionRebuildCheckpointStore>();
            IProjectionCheckpointTracker checkpointTracker = scope.ServiceProvider
                .GetRequiredService<IProjectionCheckpointTracker>();

            // Discover known (tenant, domain) pairs from the active-pair index first, then merge
            // projection-tracker identities as a compatibility fallback for rows created before
            // the pair index existed.
            var pairs = new HashSet<(string Tenant, string Domain)>();
            foreach ((string tenant, string domain) in await checkpointStore.ListActiveRebuildIndexPairsAsync(cancellationToken).ConfigureAwait(false)) {
                pairs.Add((tenant, domain));
            }

            await foreach (AggregateIdentity identity in checkpointTracker.EnumerateTrackedIdentitiesAsync(cancellationToken).ConfigureAwait(false)) {
                cancellationToken.ThrowIfCancellationRequested();
                pairs.Add((identity.TenantId, identity.Domain));
            }

            if (pairs.Count == 0) {
                return;
            }

            int totalCleared = 0;
            foreach ((string tenant, string domain) in pairs) {
                cancellationToken.ThrowIfCancellationRequested();
                try {
                    int cleared = await checkpointStore
                        .ClearOrphanActiveRebuildIndexEntriesAsync(tenant, domain, cancellationToken)
                        .ConfigureAwait(false);
                    totalCleared += cleared;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                    throw;
                }
                catch (Exception ex) when (ex is not OperationCanceledException) {
                    Log.CleanupPairFailed(logger, ex, tenant, domain, ex.GetType().Name);
                }
            }

            if (totalCleared > 0) {
                Log.CleanupSweepCompleted(logger, pairs.Count, totalCleared);
            }
        }
        finally {
            await scope.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 1200,
            Level = LogLevel.Information,
            Message = "Active-rebuild-index cleanup service started: CadenceSeconds={CadenceSeconds}, Stage=ActiveRebuildIndexCleanupServiceStarted")]
        public static partial void CleanupServiceStarted(ILogger logger, int cadenceSeconds);

        [LoggerMessage(
            EventId = 1201,
            Level = LogLevel.Information,
            Message = "Active-rebuild-index cleanup service disabled (cadence <= 0). Stage=ActiveRebuildIndexCleanupServiceDisabled")]
        public static partial void CleanupServiceDisabled(ILogger logger);

        [LoggerMessage(
            EventId = 1202,
            Level = LogLevel.Warning,
            Message = "Active-rebuild-index cleanup sweep failed: ExceptionType={ExceptionType}, Stage=ActiveRebuildIndexCleanupSweepFailed")]
        public static partial void CleanupSweepFailed(ILogger logger, Exception exception, string exceptionType);

        [LoggerMessage(
            EventId = 1203,
            Level = LogLevel.Warning,
            Message = "Active-rebuild-index cleanup failed for pair: TenantId={TenantId}, Domain={Domain}, ExceptionType={ExceptionType}, Stage=ActiveRebuildIndexCleanupPairFailed")]
        public static partial void CleanupPairFailed(ILogger logger, Exception exception, string tenantId, string domain, string exceptionType);

        [LoggerMessage(
            EventId = 1204,
            Level = LogLevel.Information,
            Message = "Active-rebuild-index cleanup sweep completed: Pairs={Pairs}, Cleared={Cleared}, Stage=ActiveRebuildIndexCleanupSweepCompleted")]
        public static partial void CleanupSweepCompleted(ILogger logger, int pairs, int cleared);
    }
}
