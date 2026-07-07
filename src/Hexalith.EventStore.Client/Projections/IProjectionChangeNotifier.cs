
namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Notifies the EventStore that a projection's read model has changed,
/// triggering ETag regeneration for cache invalidation.
/// </summary>
/// <remarks>
/// Client package defines the interface with zero infrastructure dependencies.
/// Server package provides the DAPR implementation (<c>DaprProjectionChangeNotifier</c>).
/// </remarks>
public interface IProjectionChangeNotifier {
    /// <summary>
    /// Notifies that a projection has changed for a given tenant.
    /// </summary>
    /// <param name="projectionType">The projection type name (kebab-case).</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="entityId">Optional entity identifier for future fine-grained invalidation (FR58).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyProjectionChangedAsync(
        string projectionType,
        string tenantId,
        string? entityId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies that a projection has changed using optional scoped detail metadata.
    /// </summary>
    /// <param name="detail">The projection change detail.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyProjectionChangedAsync(
        ProjectionChangedDetail detail,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(detail);
        throw new NotSupportedException(
            "This projection change notifier implementation does not support metadata-rich detail notifications. " +
            "Override NotifyProjectionChangedAsync(ProjectionChangedDetail, CancellationToken) to preserve group scope and metadata.");
    }
}
