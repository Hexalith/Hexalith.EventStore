namespace Hexalith.EventStore.SignalRHub;

/// <summary>
/// Strongly-typed SignalR client interface for projection change signals.
/// Used with <c>Hub&lt;IProjectionChangedClient&gt;</c> for compile-time safety.
/// </summary>
public interface IProjectionChangedClient {
    /// <summary>
    /// Receives a signal that a projection has changed for a given tenant.
    /// Signal-only: carries the projection type and tenant ID, not projection data.
    /// </summary>
    /// <param name="projectionType">The projection type name (kebab-case).</param>
    /// <param name="tenantId">The tenant identifier.</param>
    Task ProjectionChanged(string projectionType, string tenantId);

    /// <summary>
    /// Receives a metadata-rich, optionally scoped signal that a projection has changed.
    /// Metadata-only: carries opaque key/value metadata, never authoritative projection content.
    /// </summary>
    /// <param name="projectionType">The projection type name (kebab-case).</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="groupScope">
    /// Optional sub-tenant group scope (for example, a conversation id) that the message was
    /// scoped to; <see langword="null"/> for a tenant-wide broadcast.
    /// </param>
    /// <param name="metadata">Opaque, bounded metadata key/value pairs.</param>
    Task ProjectionChangedDetail(
        string projectionType,
        string tenantId,
        string? groupScope,
        IReadOnlyDictionary<string, string> metadata);
}
