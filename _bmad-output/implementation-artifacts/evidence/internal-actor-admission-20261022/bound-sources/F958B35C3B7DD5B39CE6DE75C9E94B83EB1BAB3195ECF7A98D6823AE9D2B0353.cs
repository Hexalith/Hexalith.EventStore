namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Single-snapshot DAPR infrastructure overview for the Admin <c>/dapr</c> page.
/// </summary>
/// <param name="Sidecar">The sidecar summary built from the same server-side query scope as <paramref name="Components"/>.</param>
/// <param name="Components">The canonical component inventory built from one <see cref="DaprCanonicalInventory"/> sample.</param>
public record DaprInfrastructureOverview(
    DaprSidecarInfo? Sidecar,
    IReadOnlyList<DaprComponentDetail> Components) {
    /// <summary>Gets the canonical components for this snapshot.</summary>
    public IReadOnlyList<DaprComponentDetail> Components { get; } = Components
        ?? throw new ArgumentNullException(nameof(Components));
}
