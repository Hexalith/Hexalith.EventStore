
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
    /// Gets the signed publisher provenance: a short-lived workload assertion from the trusted JWT issuer that
    /// names the publishing workload and grants the projection-notify operation. When the issuer can bind
    /// resource claims, the assertion is also bound to this notification's tenant, projection type, and topic.
    /// The receiver performs no ETag regeneration or broadcast unless the provenance validates and matches.
    /// </summary>
    /// <remarks>The value is an opaque credential: it is never logged, echoed, or displayed.</remarks>
    public string? Provenance { get; init; }

    /// <summary>
    /// Prints the notification members with the provenance credential redacted.
    /// </summary>
    /// <param name="builder">The builder receiving the members.</param>
    /// <returns><see langword="true"/> because members were printed.</returns>
    protected virtual bool PrintMembers(System.Text.StringBuilder builder) {
        ArgumentNullException.ThrowIfNull(builder);
        _ = builder
            .Append("ProjectionType = ").Append(ProjectionType)
            .Append(", TenantId = ").Append(TenantId)
            .Append(", EntityId = ").Append(EntityId)
            .Append(", GroupScope = ").Append(GroupScope)
            .Append(", MetadataCount = ").Append(Metadata?.Count ?? 0)
            .Append(", Provenance = ").Append(Provenance is null ? "<none>" : "[REDACTED]");
        return true;
    }

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
