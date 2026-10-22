using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Server.Streams;

/// <summary>Independent private current caller/resource/method admission, separate from finite-namespace coverage qualification.
/// Missing credentials deny raw actors; actor address and stored receipt strings never authenticate a caller.</summary>
public interface ISourcePublicationOperationAuthority : IAnchoredStateTransitionAuthority
{
    /// <summary>Authenticates this independently retained original consecutive acknowledgement proof and exact current installation/complete-cut/target/delivery authority. A stored digest or caller status alone is insufficient. Omission disables resume.</summary>
    Task<bool> VerifyDispatchProgressAsync(SourcePublicationCheckpoint cut, SourcePublicationDispatchProgress progress) => Task.FromResult(false);
    /// <summary>Independently reads actual durable original source acknowledgements for the bounded advance, verifies predecessor proof and fresh complete cut/current delivery target and authority, then issues their exact conditional prefix proof. Missing, Unknown, Quarantined or changed authority denies; omission disables progress.</summary>
    Task<SourcePublicationDispatchProgress?> AuthorizeDispatchAdvanceAsync(SourcePublicationDispatchAdvance advance, SourcePublicationIndexState current) => Task.FromResult<SourcePublicationDispatchProgress?>(null);
    /// <summary>Independently authenticates the retained exact proof, every verified original protected source prefix/projection, and current complete namespace/cut authority. Stored strings/digests never grant reuse. Omission denies reconciliation resume.</summary>
    Task<bool> VerifyReconciliationProgressAsync(SourcePublicationReconciliationProgress progress) => Task.FromResult(false);
    /// <summary>Independently authenticates the exact retained original complete installation/roster/cut and protected-prefix proof against this freshly complete current cut, proves monotonic preservation of every original source and projection, and checks current private/source/installation permission and both unchanged applicable expiries. Larger heads and stored strings alone grant nothing. Omission denies finite-cut continuation.</summary>
    Task<bool> VerifyFiniteCutContinuationAsync(SourcePublicationReconciliationProgress original, SourcePublicationCut currentCut) => Task.FromResult(false);
    /// <summary>Separately admits mutation and independently verifies the exact next original source observation/protected prefix/closed projection, predecessor proof and fresh complete cut/current installation. Issues an independently retained immutable conditional proof; caller facts alone are insufficient. Zero heads require exact complete-cut proof. Omission denies.</summary>
    Task<SourcePublicationReconciliationProgress?> AuthorizeReconciliationAdvanceAsync(SourcePublicationReconciliationAdvance advance, SourcePublicationIndexState? current) => Task.FromResult<SourcePublicationReconciliationProgress?>(null);
    /// <summary>Authenticates only the exact index read.</summary>
    Task<bool> ReadIndexAsync(SourcePublicationScope scope);
    /// <summary>Authenticates the exact conditional index request, including immutable publication vector and expected revision.</summary>
    Task<bool> WriteIndexAsync(SourcePublicationIndexWrite request);
    /// <summary>Independently validates exact persisted index revision/state digest, including revision zero and the serialized null digest for initial absence. Missing nonrollback anchors deny.</summary>
    Task<bool> ValidateIndexStateAsync(SourcePublicationScope scope, long revision, string stateDigest) => Task.FromResult(false);
    /// <summary>Deprecated compatibility-only legacy anchor hook; current recoverable actors do not invoke it.
    /// Qualified implementations must implement the mandatory inherited IAnchoredStateTransitionAuthority admitted-original admission/recovery
    /// and conditional exact transition journal, including independent staging ownership, current permission and final durable-state/anchor confirmation.
    /// Implementing this legacy hook alone never enables an actor; omitted inherited proof defaults deny.</summary>
    Task<bool> RecordIndexRevisionAsync(SourcePublicationScope scope, long expectedRevision, long nextRevision, string stateDigest) => Task.FromResult(false);
    /// <summary>Authenticates only the exact installed namespace read.</summary>
    Task<bool> ReadNamespaceAsync(SourcePublicationScope scope);
    /// <summary>Authenticates exact initial inventory and independent installation receipts.</summary>
    Task<bool> InstallNamespaceAsync(SourcePublicationNamespaceState installation);
    /// <summary>Authenticates only the exact pre-create registered source/revision within this installed scope.</summary>
    Task<bool> RegisterSourceAsync(SourcePublicationScope scope, long expectedRevision, AggregateIdentity identity);
}
