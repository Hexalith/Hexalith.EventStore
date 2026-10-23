namespace Hexalith.EventStore.DomainService.Content;

/// <summary>Body-free durable upload and cleanup metadata.</summary>
internal sealed record ContentStoredUpload(ContentReceipt Receipt, string? TaskId, bool Complete, DateTimeOffset? CleanupEpoch = null, long? CleanupGeneration = null, bool Erasing = false, bool Erased = false, string? UploadId = null, bool LegalHold = false, long HoldGeneration = 0, bool Abandoned = false, string? ErasureClaimId = null, bool DeletionAuthorized = false);
