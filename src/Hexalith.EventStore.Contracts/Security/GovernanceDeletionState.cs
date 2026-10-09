namespace Hexalith.EventStore.Contracts.Security;

/// <summary>One immutable predicate request with current admission ordinal, all violation/cycle/binding history, irreversible seal and completion.</summary>
/// <param name="RequestId">Exact RequestId.</param>
/// <param name="Scope">Exact Scope.</param>
/// <param name="PredicateDigest">Exact PredicateDigest.</param>
/// <param name="Ordinal">Exact Ordinal.</param>
/// <param name="RequiredOwnerIds">Exact RequiredOwnerIds.</param>
/// <param name="ObligationIds">Exact ObligationIds.</param>
/// <param name="OwnerCycles">Exact OwnerCycles.</param>
/// <param name="Violations">Exact Violations.</param>
/// <param name="ContentBindings">Exact ContentBindings.</param>
/// <param name="SealId">Exact SealId.</param>
/// <param name="Batches">Exact Batches.</param>
/// <param name="IntegrityCompromised">Exact IntegrityCompromised.</param>
/// <param name="Completed">Exact Completed.</param>
public sealed record GovernanceDeletionState(string RequestId, GovernanceScopeV1 Scope, string PredicateDigest, long Ordinal, IReadOnlyList<string> RequiredOwnerIds, IReadOnlyList<string> ObligationIds, IReadOnlyList<GovernanceOwnerCycle> OwnerCycles, IReadOnlyList<GovernanceViolation> Violations, IReadOnlyList<GovernanceContentBinding> ContentBindings, string SealId, IReadOnlyList<GovernanceBatchState> Batches, bool IntegrityCompromised, bool Completed);
