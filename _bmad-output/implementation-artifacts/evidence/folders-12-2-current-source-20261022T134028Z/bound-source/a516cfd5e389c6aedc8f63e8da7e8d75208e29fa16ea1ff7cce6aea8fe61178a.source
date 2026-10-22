namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Exact private guard plus source/outbox/phase-state transaction. Domain decisions remain with Agents; no lookup-then-append authority is implied.</summary>
/// <param name="TenantId">Exact tenant.</param><param name="OperationId">Immutable idempotent original effect identity.</param>
/// <param name="Guard">Mandatory expected guard cell and next guard evidence.</param><param name="Targets">Exact source/outbox/phase state changes in the same qualified tenant transaction.</param>
public sealed record GuardedStateCommitRequest(string TenantId, string OperationId, GuardedStateMutation Guard,
    IReadOnlyList<GuardedStateMutation> Targets)
{
    /// <summary>Exact no-effect result only: compare the mandatory guard without changing its logical revision/body; targets must be empty.</summary>
    public bool CompareGuardWithoutMutation { get; init; }
    /// <summary>Optional exact logical operation digest for restart lookup across later guard revisions.</summary>
    public string LogicalIntentDigest { get; init; } = "";
    /// <summary>Content-free typed original result, persisted atomically with the transaction receipt.</summary>
    public byte[] Outcome { get; init; } = [];
    /// <summary>Typed owner's actual logical guard high water, distinct from the always-advancing backend CAS generation; zero preserves legacy primitive correlation.</summary>
    public long LogicalGuardHighWater { get; init; }
}
