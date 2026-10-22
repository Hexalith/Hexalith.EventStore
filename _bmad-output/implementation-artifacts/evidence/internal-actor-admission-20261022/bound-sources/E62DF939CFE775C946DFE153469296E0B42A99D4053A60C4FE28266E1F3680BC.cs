namespace Hexalith.EventStore.DomainService;

/// <summary>One bounded source execution observation.</summary>
/// <param name="Checkpoint">Latest durably validated envelope position.</param>
/// <param name="Status">Current durable disposition.</param>
/// <param name="Processed">Number of envelopes checkpointed in this call.</param>
/// <param name="ReasonCode">Bounded unresolved reason code, without payload data.</param>
public sealed record ProcessRunResult(long Checkpoint, ProcessSourceStatus Status, int Processed, string? ReasonCode)
{
    /// <summary>Whether the source still requires recovery or disposition.</summary>
    public bool Unresolved => Status != ProcessSourceStatus.Active;
}
