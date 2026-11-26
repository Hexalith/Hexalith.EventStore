namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Catalog-declared exceptions to poison capture for subscriber skips.</summary>
[Flags]
public enum EventStoreSubscriptionSkipPolicy
{
    /// <summary>Require capture before acknowledging any skip.</summary>
    None = 0,

    /// <summary>Acknowledge an event type the subscriber does not recognize.</summary>
    UnknownEventType = 1,

    /// <summary>Acknowledge a known event type with no registered handler.</summary>
    NoHandlers = 2,

    /// <summary>Acknowledge a configured payload identity mismatch on a shared topic.</summary>
    AggregateMismatch = 4,
}
