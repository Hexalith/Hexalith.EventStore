namespace Hexalith.EventStore.Admin.Abstractions.Models.Health;

/// <summary>
/// System-wide health report (FR75).
/// </summary>
/// <param name="OverallStatus">The aggregate health status across all components.</param>
/// <param name="TotalEventCount">The total number of events in the system.</param>
/// <param name="EventsPerSecond">The current event throughput.</param>
/// <param name="ErrorPercentage">The current error rate as a percentage.</param>
/// <param name="DaprComponents">Health status of all DAPR components.</param>
/// <param name="ObservabilityLinks">Deep-link URLs to observability dashboards.</param>
/// <param name="TotalEventCountStatus">
/// Per-metric availability for <see cref="TotalEventCount"/>. <see cref="SystemHealthMetricStatus.Unavailable"/>
/// means the value is meaningless and the UI must render an explicit unavailable indicator.
/// </param>
/// <param name="EventsPerSecondStatus">
/// Per-metric availability for <see cref="EventsPerSecond"/>. Defaults to <see cref="SystemHealthMetricStatus.Unavailable"/>
/// because the rolling-window source and injectable clock required for honest measurement are not yet implemented.
/// </param>
/// <param name="ErrorPercentageStatus">
/// Per-metric availability for <see cref="ErrorPercentage"/>. Defaults to <see cref="SystemHealthMetricStatus.Unavailable"/>
/// because no source is wired (the historical name conflated infrastructure errors with command rejection rate).
/// </param>
public record SystemHealthReport(
    HealthStatus OverallStatus,
    long TotalEventCount,
    double EventsPerSecond,
    double ErrorPercentage,
    IReadOnlyList<DaprComponentHealth> DaprComponents,
    ObservabilityLinks ObservabilityLinks,
    SystemHealthMetricStatus TotalEventCountStatus = SystemHealthMetricStatus.Available,
    SystemHealthMetricStatus EventsPerSecondStatus = SystemHealthMetricStatus.Unavailable,
    SystemHealthMetricStatus ErrorPercentageStatus = SystemHealthMetricStatus.Unavailable) {
    /// <summary>Gets the current event throughput.</summary>
    public double EventsPerSecond { get; } = !double.IsNaN(EventsPerSecond) && !double.IsInfinity(EventsPerSecond)
        ? EventsPerSecond
        : throw new ArgumentOutOfRangeException(nameof(EventsPerSecond), EventsPerSecond, "Value must be a finite number.");

    /// <summary>Gets the current error rate as a percentage.</summary>
    public double ErrorPercentage { get; } = !double.IsNaN(ErrorPercentage) && !double.IsInfinity(ErrorPercentage)
        ? ErrorPercentage
        : throw new ArgumentOutOfRangeException(nameof(ErrorPercentage), ErrorPercentage, "Value must be a finite number.");

    /// <summary>Gets the health status of all DAPR components.</summary>
    public IReadOnlyList<DaprComponentHealth> DaprComponents { get; } = DaprComponents ?? throw new ArgumentNullException(nameof(DaprComponents));

    /// <summary>Gets the deep-link URLs to observability dashboards.</summary>
    public ObservabilityLinks ObservabilityLinks { get; } = ObservabilityLinks ?? throw new ArgumentNullException(nameof(ObservabilityLinks));
}
