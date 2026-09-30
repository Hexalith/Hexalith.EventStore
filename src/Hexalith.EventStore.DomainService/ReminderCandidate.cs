namespace Hexalith.EventStore.DomainService;

/// <summary>One discovery candidate in a tenant's reminder index. It is a discovery aid, never truth.</summary>
/// <param name="Domain">The target stream domain.</param>
/// <param name="Aggregate">The target stream aggregate.</param>
/// <param name="ActorId">The <c>wra-</c> actor identifier.</param>
internal sealed record ReminderCandidate(string Domain, string Aggregate, string ActorId);
