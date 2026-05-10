using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

namespace Hexalith.EventStore.Admin.Abstractions.Models.Health;

/// <summary>
/// Health status of a DAPR component.
/// </summary>
/// <param name="ComponentName">The DAPR component name.</param>
/// <param name="ComponentType">The DAPR component type (e.g., state.redis, pubsub.redis).</param>
/// <param name="Status">The current health status.</param>
/// <param name="LastCheckUtc">When the health was last checked.</param>
/// <param name="Source">
/// Inventory source attribution: which sidecar or fallback supplied this component fact. Defaults to
/// <see cref="DaprComponentSource.Unavailable"/> for forward-compatibility — clients that do
/// not yet read this field continue to work, but the UI must never treat the default as canonical.
/// </param>
/// <param name="HealthEvidenceSource">
/// Health evidence attribution: which source supplied <paramref name="Status"/> and
/// <paramref name="LastCheckUtc"/>.
/// </param>
public record DaprComponentHealth(
    string ComponentName,
    string ComponentType,
    HealthStatus Status,
    DateTimeOffset LastCheckUtc,
    DaprComponentSource Source = DaprComponentSource.Unavailable,
    DaprComponentSource HealthEvidenceSource = DaprComponentSource.Unavailable) {
    /// <summary>Gets the DAPR component name.</summary>
    public string ComponentName { get; } = !string.IsNullOrWhiteSpace(ComponentName)
        ? ComponentName
        : throw new ArgumentException("ComponentName cannot be null, empty, or whitespace.", nameof(ComponentName));

    /// <summary>Gets the DAPR component type.</summary>
    public string ComponentType { get; } = !string.IsNullOrWhiteSpace(ComponentType)
        ? ComponentType
        : throw new ArgumentException("ComponentType cannot be null, empty, or whitespace.", nameof(ComponentType));
}
