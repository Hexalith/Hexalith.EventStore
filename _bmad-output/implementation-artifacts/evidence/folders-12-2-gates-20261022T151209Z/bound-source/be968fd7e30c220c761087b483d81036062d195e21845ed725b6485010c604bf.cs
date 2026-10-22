namespace Hexalith.EventStore.Contracts.Commands;

/// <summary>An exact workload command and the human or service principal it represents.</summary>
public sealed record BoundWorkloadCommandRequest(SubmitCommandRequest Command, string PrincipalId);
