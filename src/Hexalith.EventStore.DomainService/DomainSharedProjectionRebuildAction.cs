namespace Hexalith.EventStore.DomainService;

/// <summary>One explicit transition in a shared-projection rebuild session.</summary>
public enum DomainSharedProjectionRebuildAction {
    /// <summary>Creates an empty operation-scoped candidate.</summary>
    Begin = 0,

    /// <summary>Accumulates one complete aggregate history in authoritative ordinal order.</summary>
    Accumulate = 1,

    /// <summary>Seals the authoritative inventory and prepares its immutable manifest.</summary>
    Finalize = 2,

    /// <summary>Stages the finalized immutable manifest without changing the live view.</summary>
    Stage = 3,

    /// <summary>Atomically promotes and verifies the staged immutable manifest.</summary>
    Commit = 4,

    /// <summary>Reads back durable staging or commit evidence.</summary>
    Verify = 5,

    /// <summary>Aborts accumulation or restores the pre-commit live view.</summary>
    Abort = 6,

    /// <summary>Stages one bounded, retry-safe authoritative owner event page.</summary>
    AccumulatePart = 7,

    /// <summary>Folds a complete staged owner history into the candidate.</summary>
    AccumulateComplete = 8,

    /// <summary>Stages one bounded and retry-safe page of the sealed Begin owner inventory.</summary>
    BeginPart = 9,

    /// <summary>Reads a durable session phase without advancing its lifecycle.</summary>
    Inspect = 10,
}
