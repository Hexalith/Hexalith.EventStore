namespace Hexalith.EventStore.Client.Streams;

/// <summary>Exact source acknowledgement classification; missing adapters/authority fail unavailable.</summary>
public enum SourcePublicationDeliveryStatus
{
    /// <summary>No authenticated persisted original source acknowledgement exists.</summary>
    Unavailable = 0,
    /// <summary>The exact original source acknowledgement has been independently authenticated and authoritatively replayed.</summary>
    Acknowledged = 1,
    /// <summary>The durable original is poisoned and requires independent resolution; it cannot be skipped.</summary>
    Quarantined = 2,
}
