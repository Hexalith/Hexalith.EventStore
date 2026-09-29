namespace Hexalith.EventStore.DomainService;

/// <summary>The persisted discovery candidates of one tenant.</summary>
/// <param name="Tenant">The canonical tenant.</param>
/// <param name="Candidates">The candidates, ordered by actor identifier.</param>
internal sealed record ReminderTenantCandidates(string Tenant, IReadOnlyList<ReminderCandidate> Candidates);
