namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Only an independently retained original blocked disposition; never a dispatch or physical effect receipt.</summary>
/// <param name="Original">Exact original no-effect reconciliation.</param><param name="Outcome">Its original immutable owner outcome.</param>
public sealed record DeletionBlockedReplacementResult(DeletionBlockedReplacementReconciliation Original, DeletionConsumptionOutcome Outcome);
