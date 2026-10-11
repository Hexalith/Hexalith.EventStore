namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>An exact owner query and requester carried only on the workload authority route.</summary>
public sealed record BoundOwnerQueryRequest(SubmitQueryRequest Query, string PrincipalId);
