namespace Hexalith.EventStore.Client.Gateway;

/// <summary>Operation names the gateway client requests for workload transport.</summary>
public static class EventStoreGatewayWorkloadOperations
{
    /// <summary>Command submission operation.</summary>
    public const string CommandSubmit = "eventstore:command-submit";

    /// <summary>Command status read operation.</summary>
    public const string CommandStatus = "eventstore:command-status";

    /// <summary>Stream read operation.</summary>
    public const string StreamRead = "eventstore:stream-read";
}
