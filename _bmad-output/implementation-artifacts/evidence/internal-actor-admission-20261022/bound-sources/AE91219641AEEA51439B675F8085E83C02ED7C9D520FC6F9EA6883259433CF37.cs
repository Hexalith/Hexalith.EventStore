namespace Hexalith.EventStore.Contracts.Commands;

/// <summary>Reserved command metadata stamped only after gateway workload authentication.</summary>
public static class EventStoreGatewayVerifiedOrigin
{
    /// <summary>The extension key carrying the verified workload identity.</summary>
    public const string ExtensionKey = "eventstore:verified-origin";

    /// <summary>The extension key carrying the actor bound in the verified assertion, when present.</summary>
    public const string ActorExtensionKey = "eventstore:verified-actor";
}
