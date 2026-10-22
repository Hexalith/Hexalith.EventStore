namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Summary information about the DAPR sidecar runtime.
/// </summary>
/// <param name="AppId">The DAPR app ID of the local Admin sidecar.</param>
/// <param name="RuntimeVersion">The DAPR runtime version string.</param>
/// <param name="ComponentCount">The number of registered components on the local Admin sidecar.</param>
/// <param name="SubscriptionCount">
/// The number of active subscriptions on the remote EventStore sidecar.
/// Only meaningful when <paramref name="RemoteMetadataStatus"/> is <see cref="RemoteMetadataStatus.Available"/>.
/// </param>
/// <param name="HttpEndpointCount">
/// The number of registered HTTP endpoints on the remote EventStore sidecar.
/// Only meaningful when <paramref name="RemoteMetadataStatus"/> is <see cref="RemoteMetadataStatus.Available"/>.
/// </param>
/// <param name="RemoteMetadataStatus">
/// The outcome of the remote EventStore sidecar metadata query used to populate
/// <paramref name="SubscriptionCount"/> and <paramref name="HttpEndpointCount"/>:
/// <see cref="RemoteMetadataStatus.NotConfigured"/> when no endpoint is configured,
/// <see cref="RemoteMetadataStatus.Available"/> when the call succeeded,
/// or <see cref="RemoteMetadataStatus.Unreachable"/> when the call failed with an exception.
/// </param>
/// <param name="RemoteEndpoint">
/// The attempted remote endpoint URL (from <c>AdminServer:EventStoreDaprHttpEndpoint</c>).
/// <c>null</c> only when <paramref name="RemoteMetadataStatus"/> is <see cref="RemoteMetadataStatus.NotConfigured"/>.
/// </param>
public record DaprSidecarInfo(
    string AppId,
    string RuntimeVersion,
    int ComponentCount,
    int SubscriptionCount,
    int HttpEndpointCount,
    RemoteMetadataStatus RemoteMetadataStatus,
    string? RemoteEndpoint) {
    /// <summary>Gets the DAPR app ID.</summary>
    public string AppId { get; } = !string.IsNullOrWhiteSpace(AppId)
        ? AppId
        : throw new ArgumentException("AppId cannot be null, empty, or whitespace.", nameof(AppId));

    /// <summary>Gets the DAPR runtime version string.</summary>
    public string RuntimeVersion { get; } = !string.IsNullOrWhiteSpace(RuntimeVersion)
        ? RuntimeVersion
        : throw new ArgumentException("RuntimeVersion cannot be null, empty, or whitespace.", nameof(RuntimeVersion));
}
