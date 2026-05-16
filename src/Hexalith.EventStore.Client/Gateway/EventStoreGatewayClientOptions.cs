namespace Hexalith.EventStore.Client.Gateway;

/// <summary>
/// Options for the HTTP EventStore gateway client.
/// </summary>
public sealed class EventStoreGatewayClientOptions {
    /// <summary>
    /// Gets or sets the EventStore gateway base address.
    /// </summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>
    /// Gets or sets the relative command submission endpoint path.
    /// </summary>
    public string CommandPath { get; set; } = "api/v1/commands";

    /// <summary>
    /// Gets or sets the relative query endpoint path.
    /// </summary>
    public string QueryPath { get; set; } = "api/v1/queries";

    /// <summary>
    /// Gets or sets the relative public stream read endpoint path.
    /// </summary>
    public string StreamReadPath { get; set; } = "api/v1/streams/read";

    /// <summary>
    /// Gets or sets the maximum buffered stream-read response size in bytes.
    /// </summary>
    public long MaxStreamReadResponseBytes { get; set; } = 16 * 1024 * 1024;
}
