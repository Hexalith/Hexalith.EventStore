namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Overview of DAPR pub/sub infrastructure including components, subscriptions, and remote metadata availability.
/// </summary>
/// <param name="PubSubComponents">The pub/sub components filtered from DAPR metadata.</param>
/// <param name="Subscriptions">The merged subscriptions from local and remote sidecars.</param>
/// <param name="RemoteMetadataStatus">
/// The outcome of the remote EventStore sidecar metadata query:
/// <see cref="RemoteMetadataStatus.NotConfigured"/> when no endpoint is configured,
/// <see cref="RemoteMetadataStatus.Available"/> when the call succeeded,
/// or <see cref="RemoteMetadataStatus.Unreachable"/> when the call failed with an exception.
/// </param>
/// <param name="RemoteEndpoint">
/// The attempted remote endpoint URL (from <c>AdminServer:EventStoreDaprHttpEndpoint</c>).
/// <c>null</c> only when <paramref name="RemoteMetadataStatus"/> is <see cref="RemoteMetadataStatus.NotConfigured"/>.
/// </param>
public record DaprPubSubOverview(
    IReadOnlyList<DaprComponentDetail> PubSubComponents,
    IReadOnlyList<DaprSubscriptionInfo> Subscriptions,
    RemoteMetadataStatus RemoteMetadataStatus,
    string? RemoteEndpoint)
{
    /// <summary>Gets the pub/sub components.</summary>
    public IReadOnlyList<DaprComponentDetail> PubSubComponents { get; } = PubSubComponents
        ?? throw new ArgumentNullException(nameof(PubSubComponents));

    /// <summary>Gets the subscriptions.</summary>
    public IReadOnlyList<DaprSubscriptionInfo> Subscriptions { get; } = Subscriptions
        ?? throw new ArgumentNullException(nameof(Subscriptions));
}
