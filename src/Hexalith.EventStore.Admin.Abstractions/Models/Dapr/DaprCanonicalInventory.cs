namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Canonical DAPR inventory consumed by every Admin surface that renders component or pub/sub
/// facts (<c>/health</c>, <c>/dapr</c>, <c>/dapr/pubsub</c>, <c>/dapr/health-history</c>).
/// </summary>
/// <remarks>
/// <para>The inventory merges remote EventStore sidecar metadata (canonical for loaded
/// EventStore-sidecar components and active pub/sub subscriptions) with local Admin.Server
/// dependency probes (canonical for Admin.Server dependency usability such as the state-store
/// probe). Local Admin sidecar metadata is a degraded fallback only when the remote source
/// cannot supply the fact.</para>
/// <para>Components are merged deterministically by <c>{ ComponentName, ComponentType }</c>
/// and each entry preserves <see cref="DaprComponentDetail.Source"/> attribution so a reviewer
/// can answer "which sidecar or probe supplied this fact?" without inspecting logs.</para>
/// </remarks>
/// <param name="Components">The merged canonical component list.</param>
/// <param name="PubSubSubscriptions">
/// Active pub/sub subscriptions reported by the same metadata payload that produced the
/// pub/sub components in <paramref name="Components"/>. Empty when remote metadata was not
/// available.
/// </param>
/// <param name="RemoteMetadataStatus">
/// The outcome of the remote EventStore sidecar metadata query. Defaults to
/// <see cref="Dapr.RemoteMetadataStatus.NotConfigured"/>.
/// </param>
/// <param name="RemoteEndpoint">
/// The attempted remote endpoint URL (from <c>AdminServer:EventStoreDaprHttpEndpoint</c>).
/// <c>null</c> when <paramref name="RemoteMetadataStatus"/> is
/// <see cref="Dapr.RemoteMetadataStatus.NotConfigured"/>.
/// </param>
/// <param name="LocalProbeAvailable">
/// Whether the local Admin.Server DAPR sidecar metadata API responded successfully. The
/// per-component state-store probe result lives on each <see cref="DaprComponentDetail.Status"/>
/// row instead. <c>ComputeOverallStatus</c> in <c>DaprHealthQueryService</c> uses this flag to
/// decide that admin operations cannot be served when the local sidecar metadata is missing.
/// Defaults to <c>false</c> so absent local evidence cannot be misread as healthy.
/// </param>
/// <param name="CapturedAtUtc">When this canonical sample was assembled.</param>
public record DaprCanonicalInventory(
    IReadOnlyList<DaprComponentDetail> Components,
    IReadOnlyList<DaprSubscriptionInfo> PubSubSubscriptions,
    RemoteMetadataStatus RemoteMetadataStatus,
    string? RemoteEndpoint,
    bool LocalProbeAvailable,
    DateTimeOffset CapturedAtUtc) {
    /// <summary>Gets the merged components.</summary>
    public IReadOnlyList<DaprComponentDetail> Components { get; } = Components
        ?? throw new ArgumentNullException(nameof(Components));

    /// <summary>Gets the active pub/sub subscriptions.</summary>
    public IReadOnlyList<DaprSubscriptionInfo> PubSubSubscriptions { get; } = PubSubSubscriptions
        ?? throw new ArgumentNullException(nameof(PubSubSubscriptions));

    /// <summary>An empty inventory representing a fully unavailable canonical source.</summary>
    public static DaprCanonicalInventory Empty { get; } = new(
        [], [], RemoteMetadataStatus.NotConfigured, null, false, DateTimeOffset.MinValue);
}
