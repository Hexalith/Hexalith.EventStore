namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Continuously installed stable content guard binding and its original predecessor; every intervening invalidation remains independently visible.</summary>
/// <param name="Ordinal">Exact Ordinal.</param>
/// <param name="GlobalCutId">Exact GlobalCutId.</param>
/// <param name="TokenId">Exact TokenId.</param>
/// <param name="BindingId">Exact BindingId.</param>
/// <param name="PredecessorBindingId">Exact PredecessorBindingId.</param>
public sealed record GovernanceContentBinding(long Ordinal, string GlobalCutId, string TokenId, string BindingId, string PredecessorBindingId);
