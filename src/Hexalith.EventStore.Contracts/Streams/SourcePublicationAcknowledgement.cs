namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>An exact independently authenticated durable original receipt. Submission acceptance or an unknown result cannot construct it.</summary>
/// <param name="Entry">The immutable exact source publication.</param><param name="DeliveryTarget">The installed exact receiver target.</param>
/// <param name="DeliveryAuthorityRevision">The current delivery permission revision.</param><param name="ProtectedDeletionRevision">The positive persisted receiver deletion revision.</param>
/// <param name="ReceiptId">The stable original receipt identity returned independently by both source and receiver lookup.</param>
public sealed record SourcePublicationAcknowledgement(SourcePublicationIndexEntry Entry, string DeliveryTarget,
    string DeliveryAuthorityRevision, long ProtectedDeletionRevision, string ReceiptId);
