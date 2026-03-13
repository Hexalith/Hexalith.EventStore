
namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>
/// Notification raised when a projection's read model changes.
/// Used to trigger ETag regeneration for cache invalidation.
/// </summary>
/// <param name="ProjectionType">The kebab-case projection type name (e.g., "order-list").</param>
/// <param name="TenantId">The tenant identifier (kebab-case).</param>
/// <param name="EntityId">Optional entity identifier for future fine-grained invalidation (FR58).</param>
public record ProjectionChangedNotification(
    string ProjectionType,
    string TenantId,
    string? EntityId = null);
