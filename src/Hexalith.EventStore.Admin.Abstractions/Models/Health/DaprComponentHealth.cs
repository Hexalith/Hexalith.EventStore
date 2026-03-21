namespace Hexalith.EventStore.Admin.Abstractions.Models.Health;

/// <summary>
/// Health status of a DAPR component.
/// </summary>
/// <param name="ComponentName">The DAPR component name.</param>
/// <param name="ComponentType">The DAPR component type (e.g., state.redis, pubsub.redis).</param>
/// <param name="Status">The current health status.</param>
/// <param name="LastCheckUtc">When the health was last checked.</param>
public record DaprComponentHealth(string ComponentName, string ComponentType, HealthStatus Status, DateTimeOffset LastCheckUtc)
{
    /// <summary>Gets the DAPR component name.</summary>
    public string ComponentName { get; } = !string.IsNullOrWhiteSpace(ComponentName)
        ? ComponentName
        : throw new ArgumentException("ComponentName cannot be null, empty, or whitespace.", nameof(ComponentName));

    /// <summary>Gets the DAPR component type.</summary>
    public string ComponentType { get; } = !string.IsNullOrWhiteSpace(ComponentType)
        ? ComponentType
        : throw new ArgumentException("ComponentType cannot be null, empty, or whitespace.", nameof(ComponentType));
}
