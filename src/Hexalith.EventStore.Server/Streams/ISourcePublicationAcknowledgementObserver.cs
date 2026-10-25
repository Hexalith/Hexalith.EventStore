using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Server.Streams;

/// <summary>Separate authenticated source and receiver lookup ports, using independently enrolled credentials and actual durable originals.</summary>
public interface ISourcePublicationAcknowledgementObserver
{
    /// <summary>Reads the original persisted source acknowledgement. Unknown, missing and quarantined results return null.</summary>
    Task<SourcePublicationAcknowledgement?> ReadSourceAsync(SourcePublicationIndexEntry entry, CancellationToken cancellationToken = default);
    /// <summary>Independently reads the original persisted receiver result; this never calls submission or the requesting index/namespace.</summary>
    Task<SourcePublicationAcknowledgement?> ReadReceiverAsync(SourcePublicationIndexEntry entry, CancellationToken cancellationToken = default);
}
