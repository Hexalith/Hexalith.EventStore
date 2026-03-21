namespace Hexalith.EventStore.Admin.Abstractions.Models.Health;

/// <summary>
/// Overall health status of a system component.
/// </summary>
public enum HealthStatus
{
    /// <summary>The component is operating normally.</summary>
    Healthy,

    /// <summary>The component is operational but experiencing issues.</summary>
    Degraded,

    /// <summary>The component is not operational.</summary>
    Unhealthy,
}
