namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Overview of DAPR pub/sub infrastructure including components, subscriptions, and remote metadata availability.
/// </summary>
/// <param name="PubSubComponents">The pub/sub components filtered from DAPR metadata.</param>
/// <param name="Subscriptions">The merged subscriptions from local and remote sidecars.</param>
/// <param name="IsRemoteMetadataAvailable">Whether subscription data was successfully retrieved from the EventStore server sidecar.</param>
public record DaprPubSubOverview(
    IReadOnlyList<DaprComponentDetail> PubSubComponents,
    IReadOnlyList<DaprSubscriptionInfo> Subscriptions,
    bool IsRemoteMetadataAvailable)
{
    /// <summary>Gets the pub/sub components.</summary>
    public IReadOnlyList<DaprComponentDetail> PubSubComponents { get; } = PubSubComponents
        ?? throw new ArgumentNullException(nameof(PubSubComponents));

    /// <summary>Gets the subscriptions.</summary>
    public IReadOnlyList<DaprSubscriptionInfo> Subscriptions { get; } = Subscriptions
        ?? throw new ArgumentNullException(nameof(Subscriptions));
}
