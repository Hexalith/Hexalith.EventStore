using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

/// <summary>Reads durable recovery coverage and bounded reason counts for readiness.</summary>
public sealed class ProcessReadinessHealthCheck(
    ProcessSweepStore sweeps,
    IOptions<EventStoreProcessOptions> options,
    TimeProvider time,
    ProcessReconciler? reconciler = null) : IHealthCheck
{
    private readonly ProcessSweepStore _sweeps = sweeps ?? throw new ArgumentNullException(nameof(sweeps));
    private readonly EventStoreProcessOptions _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
    private readonly TimeProvider _time = time ?? throw new ArgumentNullException(nameof(time));
    private readonly ProcessReconciler? _reconciler = reconciler;

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(_options.OperationTimeout);
            ProcessSweepState state = await _sweeps.GetAsync(deadline.Token).WaitAsync(_options.OperationTimeout, cancellationToken).ConfigureAwait(false);
            bool capacityExceeded = state.RetainedCandidates > _options.SupportedCandidateCount;
            string? localFailure = _reconciler?.LocalFailureReason;
            var reasons = new Dictionary<string, int>(state.Reasons, StringComparer.Ordinal);
            if (localFailure is not null) reasons[localFailure] = Math.Max(1, reasons.GetValueOrDefault(localFailure));
            var data = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["cycle"] = state.Cycle,
                ["complete"] = state.Complete,
                ["unresolved"] = state.Unresolved,
                ["pendingDispositions"] = state.Pending.Count,
                ["pendingPublications"] = state.PendingPublications.Count,
                ["pendingTransitions"] = state.PendingTransitions.Count,
                ["retainedCandidates"] = state.RetainedCandidates,
                ["capacityExceeded"] = capacityExceeded,
                ["reasonCounts"] = reasons,
            };
            bool fresh = state.LastCompleteAt is { } completed && completed <= _time.GetUtcNow()
                && _time.GetUtcNow() - completed <= _options.ReadinessMaxAge;
            return state.Complete && state.Unresolved == 0 && state.Pending.Count == 0 && state.PendingPublications.Count == 0
                && state.PendingTransitions.Count == 0
                && !capacityExceeded && fresh && localFailure is null && _options.ReconciliationEnabled && (_reconciler?.IsRunning ?? true)
                ? HealthCheckResult.Healthy("Process recovery is complete.", data)
                : HealthCheckResult.Degraded("Process recovery is incomplete, unresolved, or overdue.", data: data);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Degraded("Process recovery state is unavailable.");
        }
    }
}
