using Hexalith.EventStore.Admin.Abstractions.Models.Health;

namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// A single health observation for a DAPR component at a specific point in time.
/// </summary>
/// <param name="ComponentName">The DAPR component name.</param>
/// <param name="ComponentType">The DAPR component type (e.g., state.redis, pubsub.redis).</param>
/// <param name="Status">The health status at capture time.</param>
/// <param name="CapturedAtUtc">When the health snapshot was captured.</param>
public record DaprHealthHistoryEntry(
    string ComponentName,
    string ComponentType,
    HealthStatus Status,
    DateTimeOffset CapturedAtUtc)
{
    /// <summary>Gets the DAPR component name.</summary>
    public string ComponentName { get; } = ComponentName ?? string.Empty;

    /// <summary>Gets the DAPR component type.</summary>
    public string ComponentType { get; } = ComponentType ?? string.Empty;
}
