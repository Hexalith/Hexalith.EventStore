namespace Hexalith.EventStore.DomainService;

/// <summary>Durable phase of one shared-projection rebuild session.</summary>
public enum DomainSharedProjectionRebuildPhase {
    /// <summary>The empty candidate may accept authoritative aggregate histories.</summary>
    Accumulating = 0,

    /// <summary>The authoritative inventory is sealed and the candidate is immutable.</summary>
    Finalized = 1,

    /// <summary>The immutable manifest is staged while the previous view remains live.</summary>
    Prepared = 2,

    /// <summary>The immutable manifest is durably visible and verified.</summary>
    Committed = 3,

    /// <summary>The operation is terminal and the pre-commit live view is preserved.</summary>
    Aborted = 4,
}
