namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Bounded exact prospective state retained before an independent conditional anchor advances. It is never itself authority.</summary>
/// <param name="ScopeId">Exact owner actor and private state purpose.</param>
/// <param name="ExpectedRevision">Authenticated predecessor revision.</param>
/// <param name="TargetRevision">Conditional successor revision.</param>
/// <param name="PredecessorDigest">Exact predecessor state digest.</param>
/// <param name="TargetDigest">Exact target state digest.</param>
/// <param name="TargetBytes">Owned serialized prospective bytes; no unanchored state is released.</param>
public sealed record AnchoredStateTransition(string ScopeId, long ExpectedRevision, long TargetRevision,
    string PredecessorDigest, string TargetDigest, byte[] TargetBytes);
