using Microsoft.AspNetCore.SignalR.Client;

namespace Hexalith.EventStore.SignalR;

/// <summary>
/// Configuration options for <see cref="EventStoreSignalRClient"/>.
/// </summary>
public class EventStoreSignalRClientOptions {
    /// <summary>
    /// Gets the absolute URL of the projection-changes SignalR hub.
    /// Example: <c>https://localhost:5001/hubs/projection-changes</c>
    /// or Aspire service reference: <c>https+http://commandapi/hubs/projection-changes</c>.
    /// </summary>
    public required string HubUrl { get; init; }

    /// <summary>
    /// Gets an optional access token provider used when the SignalR hub requires bearer authentication.
    /// </summary>
    public Func<Task<string?>>? AccessTokenProvider { get; init; }

    /// <summary>
    /// Gets an optional custom retry policy for automatic reconnection.
    /// When <c>null</c>, the SignalR default policy is used: retries at [0s, 2s, 10s, 30s] then permanently disconnects (~42s).
    /// For long-running Blazor Server apps, consider providing an infinite-retry policy with exponential backoff.
    /// </summary>
    public IRetryPolicy? RetryPolicy { get; init; }
}
