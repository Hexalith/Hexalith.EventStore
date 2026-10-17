namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// The protocol phases at which a deterministic fault can be injected. Used only by the in-memory fake to
/// force partial progress, crashes, and post-dispatch cancellation for tests; the DAPR adapter never
/// injects.
/// </summary>
public enum ReadModelBatchPhase {
    /// <summary>Before any state access.</summary>
    BeforeDispatch,

    /// <summary>After the prepared marker is durable.</summary>
    MarkerPrepared,

    /// <summary>Before installing a pending operation (ordinal supplied).</summary>
    BeforeInstallOperation,

    /// <summary>After installing a pending operation (ordinal supplied).</summary>
    AfterInstallOperation,

    /// <summary>Before the commit marker transition (the visibility flip).</summary>
    BeforeCommit,

    /// <summary>After the commit marker transition.</summary>
    AfterCommit,

    /// <summary>Before compacting a committed operation (ordinal supplied).</summary>
    BeforeCompaction,

    /// <summary>After compacting a committed operation (ordinal supplied).</summary>
    AfterCompaction,

    /// <summary>Before writing the terminal completion receipt.</summary>
    BeforeReceipt,

    /// <summary>Inside durable reconciliation after an ambiguous dispatch.</summary>
    Reconcile,

    /// <summary>After the opted-in terminal receipt, while the final own envelope is still discoverable.</summary>
    RecoveryReceiptWritten,
    /// <summary>After reclaiming the opted-in manifest, before compacting the final own envelope.</summary>
    RecoveryManifestReclaimed,
    /// <summary>After the final opted-in envelope is compacted/restored.</summary>
    RecoveryAnchorCompacted,
}
