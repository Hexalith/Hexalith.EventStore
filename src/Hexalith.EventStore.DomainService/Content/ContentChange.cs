namespace Hexalith.EventStore.DomainService.Content;

/// <summary>One immutable version binding or incarnation tombstone.</summary>
/// <param name="FileId">Opaque file identity.</param>
/// <param name="IncarnationId">Never-reused incarnation identity.</param>
/// <param name="Version">Strictly increasing file version.</param>
/// <param name="ReceiptId">Verified upload receipt; null removes the entire incarnation.</param>
/// <param name="ResourceMetadata">Bounded non-confidential immutable resource metadata; authority validates before persistence.</param>
/// <param name="ExpectedReceipt">Full verified metadata, required for body bindings and compared to stored upload metadata.</param>
public sealed record ContentChange(string FileId, string IncarnationId, long Version, string? ReceiptId, IReadOnlyDictionary<string, string>? ResourceMetadata = null, ContentReceipt? ExpectedReceipt = null);
