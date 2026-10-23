namespace Hexalith.EventStore.DomainService.Content;

/// <summary>Body-free recovery and disposition evidence for an upload.</summary>
/// <param name="Receipt">Immutable receipt.</param>
/// <param name="TaskId">Assigned task or null.</param>
/// <param name="Complete">Actual bytes were durably verified.</param>
/// <param name="Abandoned">Never-assigned upload has a durable terminal abandonment.</param>
/// <param name="LegalHold">Content-specific physical-preservation hold.</param>
/// <param name="CleanupEpoch">Start of the current qualifying P7D epoch.</param>
/// <param name="Erased">Physical bytes were erased.</param>
public sealed record ContentUploadStatus(ContentReceipt Receipt, string? TaskId, bool Complete, bool Abandoned, bool LegalHold, DateTimeOffset? CleanupEpoch, bool Erased);
