
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>
/// Notification raised when a projection's read model changes.
/// Used to trigger ETag regeneration for cache invalidation.
/// </summary>
/// <param name="ProjectionType">The kebab-case projection type name (e.g., "order-list").</param>
/// <param name="TenantId">The tenant identifier (kebab-case).</param>
/// <param name="EntityId">Optional entity identifier for future fine-grained invalidation (FR58).</param>
/// <param name="GroupScope">Optional sub-tenant SignalR group scope for detail notifications.</param>
/// <param name="Metadata">Optional opaque metadata for detail notifications.</param>
[method: JsonConstructor]
public record ProjectionChangedNotification(
    string ProjectionType,
    string TenantId,
    string? EntityId = null,
    string? GroupScope = null,
    IReadOnlyDictionary<string, string>? Metadata = null) {
    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionChangedNotification"/> record
    /// using the legacy signal-only constructor shape.
    /// </summary>
    /// <param name="projectionType">The kebab-case projection type name.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    public ProjectionChangedNotification(string projectionType, string tenantId)
        : this(projectionType, tenantId, null, null, null) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionChangedNotification"/> record
    /// using the legacy entity-scoped constructor shape.
    /// </summary>
    /// <param name="projectionType">The kebab-case projection type name.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="entityId">Optional entity identifier for fine-grained invalidation.</param>
    public ProjectionChangedNotification(string projectionType, string tenantId, string? entityId)
        : this(projectionType, tenantId, entityId, null, null) {
    }

    /// <summary>
    /// Deconstructs the notification using the legacy signal-only contract shape.
    /// </summary>
    /// <param name="projectionType">The kebab-case projection type name.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="entityId">Optional entity identifier for fine-grained invalidation.</param>
    public void Deconstruct(out string projectionType, out string tenantId, out string? entityId) {
        projectionType = ProjectionType;
        tenantId = TenantId;
        entityId = EntityId;
    }
}
