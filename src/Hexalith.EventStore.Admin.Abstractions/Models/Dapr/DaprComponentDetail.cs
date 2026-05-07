using Hexalith.EventStore.Admin.Abstractions.Models.Health;

namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Detailed information about a DAPR component, including category, health status, and capabilities.
/// </summary>
/// <param name="ComponentName">The DAPR component name.</param>
/// <param name="ComponentType">The DAPR component type (e.g., state.redis, pubsub.kafka).</param>
/// <param name="Category">The component category derived from the type prefix.</param>
/// <param name="Version">The component version string.</param>
/// <param name="Status">The current health status.</param>
/// <param name="LastCheckUtc">When the health was last checked.</param>
/// <param name="Capabilities">The component capabilities (e.g., ETAG, TRANSACTIONAL, TTL).</param>
/// <param name="Source">
/// Source attribution: which sidecar or probe supplied this fact. Defaults to
/// <see cref="DaprComponentSource.Unavailable"/> so legacy/wire-default values cannot silently
/// be presented as canonical evidence.
/// </param>
public record DaprComponentDetail(
    string ComponentName,
    string ComponentType,
    DaprComponentCategory Category,
    string Version,
    HealthStatus Status,
    DateTimeOffset LastCheckUtc,
    IReadOnlyList<string> Capabilities,
    DaprComponentSource Source = DaprComponentSource.Unavailable) {
    /// <summary>Gets the DAPR component name.</summary>
    public string ComponentName { get; } = !string.IsNullOrWhiteSpace(ComponentName)
        ? ComponentName
        : throw new ArgumentException("ComponentName cannot be null, empty, or whitespace.", nameof(ComponentName));

    /// <summary>Gets the DAPR component type.</summary>
    public string ComponentType { get; } = !string.IsNullOrWhiteSpace(ComponentType)
        ? ComponentType
        : throw new ArgumentException("ComponentType cannot be null, empty, or whitespace.", nameof(ComponentType));
}
