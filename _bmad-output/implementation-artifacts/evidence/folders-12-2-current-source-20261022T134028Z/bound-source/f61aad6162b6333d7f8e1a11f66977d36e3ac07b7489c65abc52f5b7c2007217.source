namespace Hexalith.EventStore.Client.Streams;

/// <summary>Exact owning transaction state, never inferred from a prior guard read.</summary>
public enum DirectoryAtomicAppendState
{
    /// <summary>No qualified transaction/current authority/result proof.</summary>
    Unavailable = 0,
    /// <summary>Target and assigned acceptance attribution durably committed in the same owner transaction.</summary>
    Accepted = 1,
    /// <summary>Owning conditional guard/epoch/target compare rejected; no target write.</summary>
    Rejected = 2,
    /// <summary>Original outcome is unresolved; only exact authenticated lookup is safe.</summary>
    Unknown = 3,
}
