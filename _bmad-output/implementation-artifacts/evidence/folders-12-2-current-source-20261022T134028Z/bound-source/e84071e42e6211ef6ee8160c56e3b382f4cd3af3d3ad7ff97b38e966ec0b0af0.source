namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Qualified atomic backend observation; an unknown result never permits a new destructive request.</summary>
public enum DeletionManifestProviderState
{
    /// <summary>Authoritative absence of any start under this exact durable reservation.</summary>
    NotStarted,
    /// <summary>Complete original irreversible vector.</summary>
    Consumed,
    /// <summary>Unknown/outage/partial/malformed; cannot certify destruction or absence.</summary>
    Unknown,
}
