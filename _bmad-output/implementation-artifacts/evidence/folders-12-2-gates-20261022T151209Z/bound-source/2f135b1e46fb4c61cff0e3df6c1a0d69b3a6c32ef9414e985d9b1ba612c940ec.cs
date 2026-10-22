using Dapr.Actors;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Server.Streams;

/// <summary>Private technical actor transport. Host registration/ACL and namespace authority must be qualified before use.</summary>
public interface ISourcePublicationIndexActor : IActor
{
    /// <summary>Reads independently authenticated original source reconciliation under the exact fresh complete cut; missing proof denies reuse.</summary>
    Task<SourcePublicationReconciliationProgress?> ReadReconciliationProgressAsync(SourcePublicationCut cut);
    /// <summary>Independently authenticates one original protected source prefix and conditionally retains exact bounded work; omission denies progress.</summary>
    Task<SourcePublicationReconciliationProgress?> AdvanceReconciliationProgressAsync(SourcePublicationReconciliationAdvance advance);
    /// <summary>Reads independent exact original acknowledgement-prefix proof under current source/cut/delivery authority.</summary>
    Task<SourcePublicationDispatchProgress?> ReadDispatchProgressAsync(SourcePublicationCheckpoint cut);
    /// <summary>Conditionally retains independently authenticated exact original acknowledgement progress in this same anchored owner.</summary>
    Task<SourcePublicationDispatchProgress?> AdvanceDispatchProgressAsync(SourcePublicationDispatchAdvance advance);
    /// <summary>Reads the exact index owned by this actor identity.</summary>
    Task<SourcePublicationIndexState?> ReadAsync(SourcePublicationScope scope);
    /// <summary>Atomically compares and commits one revision under the actor's single owner turn.</summary>
    Task<bool> TryWriteAsync(SourcePublicationIndexWrite write);
}
