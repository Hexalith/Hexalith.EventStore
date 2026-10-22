namespace Hexalith.EventStore.DomainService;

/// <summary>Durable evidence that authoritative target discovery is unfinished or failed.</summary>
/// <param name="Complete">Whether a complete validated catalog sweep finished.</param>
/// <param name="AfterKey">Last fully attempted target key; incomplete evidence retains failed publication; unfinished sweeps resume here after restart.</param>
/// <param name="ReasonCode">Bounded category for unresolved discovery.</param>
internal sealed record ReminderCatalogDiscoveryState(bool Complete, string? AfterKey, string? ReasonCode)
{
    /// <summary>Monotonic ownership generation; legacy documents start at zero.</summary>
    public long Generation { get; init; }

    /// <summary>Unique claim identity, fencing overlapping or abandoned providers.</summary>
    public string? Owner { get; init; }

    /// <summary>Monotonic transition within this claim.</summary>
    public long Revision { get; init; }

    /// <summary>Whether an earlier page in this unfinished sweep omitted unavailable scopes.</summary>
    public bool IsIncomplete { get; init; }
}
