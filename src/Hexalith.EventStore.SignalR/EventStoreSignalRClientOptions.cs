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
}
