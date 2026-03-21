using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;

namespace Hexalith.EventStore.Admin.Abstractions.Services;

/// <summary>
/// Type catalog is tenant-agnostic — event/command/aggregate types are registered globally
/// via reflection-based assembly scanning, not per-tenant. No tenantId parameter required (FR74).
/// </summary>
public interface ITypeCatalogService
{
    /// <summary>
    /// Lists all registered event types, optionally filtered by domain.
    /// </summary>
    /// <param name="domain">Optional domain filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of event type information.</returns>
    Task<IReadOnlyList<EventTypeInfo>> ListEventTypesAsync(string? domain, CancellationToken ct = default);

    /// <summary>
    /// Lists all registered command types, optionally filtered by domain.
    /// </summary>
    /// <param name="domain">Optional domain filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of command type information.</returns>
    Task<IReadOnlyList<CommandTypeInfo>> ListCommandTypesAsync(string? domain, CancellationToken ct = default);

    /// <summary>
    /// Lists all registered aggregate types, optionally filtered by domain.
    /// </summary>
    /// <param name="domain">Optional domain filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of aggregate type information.</returns>
    Task<IReadOnlyList<AggregateTypeInfo>> ListAggregateTypesAsync(string? domain, CancellationToken ct = default);
}
