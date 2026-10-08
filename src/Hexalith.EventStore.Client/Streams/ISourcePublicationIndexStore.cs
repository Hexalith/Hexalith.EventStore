using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Technical durable exact-index storage with one conditional revision owner.</summary>
public interface ISourcePublicationIndexStore
{
    /// <summary>Reads the exact installed index, or null when it has never been created.</summary>
    Task<SourcePublicationIndexState?> ReadAsync(SourcePublicationScope scope, CancellationToken cancellationToken = default);
    /// <summary>Conditionally persists revision expectedRevision+1; false requires an exact reread, never blind overwrite.</summary>
    Task<bool> TryWriteAsync(SourcePublicationScope scope, long expectedRevision, SourcePublicationIndexState state,
        CancellationToken cancellationToken = default);
}
