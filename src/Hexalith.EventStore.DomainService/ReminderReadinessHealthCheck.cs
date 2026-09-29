using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// The <c>eventstore-reminders-unresolved</c> readiness check. Unresolved, quarantined, or unscanned reminder
/// work reports <see cref="HealthStatus.Degraded"/>, which keeps <c>/ready</c> at 200 so one tenant's stranded
/// work cannot take the service out of rotation, while still surfacing the condition. A missing app-channel
/// token outside Development is a host misconfiguration that refuses every reminder actor call, so it reports
/// <see cref="HealthStatus.Unhealthy"/>, as the gateway does. Data holds counts only.
/// </summary>
internal sealed class ReminderReadinessHealthCheck(
    ReminderRuntimeStatus status,
    ReminderCallbackTokenFilter tokenFilter) : IHealthCheck
{
    private readonly ReminderRuntimeStatus _status = status ?? throw new ArgumentNullException(nameof(status));
    private readonly ReminderCallbackTokenFilter _tokenFilter = tokenFilter ?? throw new ArgumentNullException(nameof(tokenFilter));

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        ReminderRuntimeSnapshot snapshot = _status.Snapshot();
        var data = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["passCompleted"] = snapshot.PassCompleted,
            ["incompleteScans"] = snapshot.IncompleteScans,
            ["unresolvedItems"] = snapshot.UnresolvedItems,
            ["unresolved"] = snapshot.Unresolved,
            ["quarantined"] = snapshot.Quarantined,
            ["callbackTokenConfigured"] = _tokenFilter.IsConfigured,
        };

        if (!_tokenFilter.IsConfigured)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "The app-channel token is not configured; every reminder actor call fails closed.",
                data: data));
        }

        string? reason = !snapshot.PassCompleted
                ? "No reminder reconciliation pass has completed yet."
                : snapshot.IncompleteScans > 0
                    ? "The last reminder reconciliation pass was incomplete."
                    : snapshot.Quarantined > 0
                        ? "Quarantined reminder evidence awaits operator disposition."
                        : snapshot.Unresolved > 0
                            ? "Reminder work is retained without a durable outcome."
                            : null;
        return Task.FromResult(reason is null
            ? HealthCheckResult.Healthy("Reminder work is resolved.", data)
            : HealthCheckResult.Degraded(reason, data: data));
    }
}
