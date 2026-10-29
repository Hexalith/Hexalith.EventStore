namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Catalog authority cannot safely admit the operation; no fallback target is permitted.</summary>
/// <param name="reason">Public refusal description.</param>
/// <param name="category">Closed diagnostic category, containing no request or credential material.</param>
public sealed class RouteCatalogUnavailableException(string reason, RouteCatalogRefusalReason category = RouteCatalogRefusalReason.Disagreement) : InvalidOperationException(reason)
{
    /// <summary>Gets the bounded nonsecret diagnostic category.</summary>
    public RouteCatalogRefusalReason Category { get; } = category;
}
