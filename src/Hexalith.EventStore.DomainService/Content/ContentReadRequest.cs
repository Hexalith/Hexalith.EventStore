namespace Hexalith.EventStore.DomainService.Content;

/// <summary>Exact authorized version read with a caller-selected byte bound.</summary>
/// <param name="Scope">Owning scope.</param>
/// <param name="FileId">Opaque file identity.</param>
/// <param name="IncarnationId">Exact file incarnation.</param>
/// <param name="Version">Exact immutable version.</param>
/// <param name="MaximumBytes">Maximum permitted result length.</param>
public sealed record ContentReadRequest(ContentScope Scope, string FileId, string IncarnationId, long Version, long MaximumBytes);
