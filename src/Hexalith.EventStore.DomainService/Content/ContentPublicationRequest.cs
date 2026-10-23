namespace Hexalith.EventStore.DomainService.Content;

/// <summary>Whole-batch publication identity, bound to the authoritative command and fences.</summary>
/// <param name="Scope">Owning scope.</param>
/// <param name="CommandId">Authoritative command identity.</param>
/// <param name="TaskId">Owning task.</param>
/// <param name="LockFence">Current monotonic lock fence.</param>
/// <param name="ExpectedVersion">Expected owning aggregate version.</param>
/// <param name="Changes">Atomic collection of immutable version changes.</param>
public sealed record ContentPublicationRequest(ContentScope Scope, string CommandId, string TaskId, long LockFence, long ExpectedVersion, IReadOnlyList<ContentChange> Changes);
