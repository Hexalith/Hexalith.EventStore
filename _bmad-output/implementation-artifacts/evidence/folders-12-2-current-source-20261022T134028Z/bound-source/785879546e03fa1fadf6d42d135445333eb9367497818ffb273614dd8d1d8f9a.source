using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Technical durable exact-index storage with one conditional revision owner.</summary>
public interface ISourcePublicationIndexStore
{
    /// <summary>Reads independently authenticated original source reconciliation under the exact fresh complete cut; missing proof denies reuse.</summary>
    Task<SourcePublicationReconciliationProgress?> ReadReconciliationProgressAsync(SourcePublicationCut cut, CancellationToken cancellationToken = default) => Task.FromResult<SourcePublicationReconciliationProgress?>(null);
    /// <summary>Independently authenticates one original protected source prefix and conditionally retains exact bounded work; omission denies progress.</summary>
    Task<SourcePublicationReconciliationProgress?> AdvanceReconciliationProgressAsync(SourcePublicationReconciliationAdvance advance, CancellationToken cancellationToken = default) => Task.FromResult<SourcePublicationReconciliationProgress?>(null);
    /// <summary>Reads only a freshly independently authenticated retained consecutive acknowledgement prefix for the exact complete cut and current delivery binding. Omission cannot authorize resume.</summary>
    Task<SourcePublicationDispatchProgress?> ReadDispatchProgressAsync(SourcePublicationCheckpoint cut, CancellationToken cancellationToken = default) => Task.FromResult<SourcePublicationDispatchProgress?>(null);
    /// <summary>Independently verifies actual original acknowledgements and conditionally retains exact consecutive progress; caller status strings cannot authorize advancement. Omission denies durable progress.</summary>
    Task<SourcePublicationDispatchProgress?> AdvanceDispatchProgressAsync(SourcePublicationDispatchAdvance advance, CancellationToken cancellationToken = default) => Task.FromResult<SourcePublicationDispatchProgress?>(null);
    /// <summary>Reads the exact installed index, or null when it has never been created.</summary>
    Task<SourcePublicationIndexState?> ReadAsync(SourcePublicationScope scope, CancellationToken cancellationToken = default);
    /// <summary>Conditionally persists revision expectedRevision+1; false requires an exact reread, never blind overwrite.</summary>
    Task<bool> TryWriteAsync(SourcePublicationScope scope, long expectedRevision, SourcePublicationIndexState state,
        CancellationToken cancellationToken = default);
}
