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
}
