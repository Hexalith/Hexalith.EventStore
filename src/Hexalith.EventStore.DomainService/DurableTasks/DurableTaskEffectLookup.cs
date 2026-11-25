namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>The authoritative result of looking up or invoking an owner effect.</summary>
public enum DurableTaskEffectLookup
{
    /// <summary>The effect is authoritatively absent and may be invoked with the same step key.</summary>
    Absent,

    /// <summary>The effect committed and the owner can return its receipt.</summary>
    Committed,

    /// <summary>An owner partial effect requires reconciliation before further execution.</summary>
    Unresolved,

    /// <summary>The owner authoritatively refused the step and holds no effect for its key. The task ends as Rejected.</summary>
    Rejected,
}
