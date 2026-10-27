namespace Hexalith.EventStore.DomainService.Content;

/// <summary>Task/fence-bound mutation authorization for the existing internal seal capability.</summary>
public sealed record ContentTaskSealRequest(string TaskId, long LeaseFence, bool Completion);
