namespace Hexalith.EventStore.DomainService.Content;

/// <summary>Expected metadata for actual bytes, verified before any durable body write.</summary>
/// <param name="Scope">Owning scope.</param>
/// <param name="TaskId">Opaque task identity, or null for an upload not yet assigned to work.</param>
/// <param name="ExpectedSha256">Expected lowercase SHA-256, without a prefix.</param>
/// <param name="ExpectedLength">Expected exact byte length.</param>
/// <param name="MediaType">Expected content type.</param>
/// <param name="UploadId">Optional stable opaque intent identity, scoped to tenant/workspace/task; equivalent retries reuse one receipt.</param>
public sealed record ContentUploadRequest(ContentScope Scope, string? TaskId, string ExpectedSha256, long ExpectedLength, string MediaType, string? UploadId = null);
