using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Private current receiver/source admission adapter. Acknowledged requires authenticated exact durable source acknowledgement; command acceptance is insufficient.</summary>
public interface ISourcePublicationDelivery
{
    /// <summary>Independently verifies the original persisted source acknowledgement or exact retained pending publication. Unknown or quarantine stops progress.</summary>
    Task<SourcePublicationDeliveryStatus> LookupAcknowledgementAsync(SourcePublicationIndexEntry entry, CancellationToken cancellationToken = default) => Task.FromResult(SourcePublicationDeliveryStatus.Unavailable);
    /// <summary>Looks up or delivers the immutable original publication; unknown/quarantined outcomes never permit checkpoint advance.</summary>
    Task<SourcePublicationDeliveryStatus> DeliverAsync(SourcePublicationIndexEntry entry, CancellationToken cancellationToken = default);
}
