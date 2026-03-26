namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Summary information about the DAPR sidecar runtime.
/// </summary>
/// <param name="AppId">The DAPR app ID.</param>
/// <param name="RuntimeVersion">The DAPR runtime version string.</param>
/// <param name="ComponentCount">The number of registered components.</param>
/// <param name="SubscriptionCount">The number of active subscriptions.</param>
/// <param name="HttpEndpointCount">The number of registered HTTP endpoints.</param>
public record DaprSidecarInfo(
    string AppId,
    string RuntimeVersion,
    int ComponentCount,
    int SubscriptionCount,
    int HttpEndpointCount)
{
    /// <summary>Gets the DAPR app ID.</summary>
    public string AppId { get; } = !string.IsNullOrWhiteSpace(AppId)
        ? AppId
        : throw new ArgumentException("AppId cannot be null, empty, or whitespace.", nameof(AppId));

    /// <summary>Gets the DAPR runtime version string.</summary>
    public string RuntimeVersion { get; } = !string.IsNullOrWhiteSpace(RuntimeVersion)
        ? RuntimeVersion
        : throw new ArgumentException("RuntimeVersion cannot be null, empty, or whitespace.", nameof(RuntimeVersion));
}
