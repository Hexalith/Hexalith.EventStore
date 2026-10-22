namespace Hexalith.EventStore.Client.Streams;

/// <summary>Complete original atomic transaction evidence.</summary>
/// <param name="OperationId">Original logical operation.</param><param name="RequestDigest">Safe exact scope/intent identity.</param><param name="State">Original transaction outcome.</param>
/// <param name="CommittedTargetRevision">Actual committed target revision, zero when rejected/unknown.</param><param name="AcceptedAtAdmissionFenceOrdinal">Maximum installed ordinal matching the write at its linearization. A violation reporter must use the exact deletion request's ordinal from the authenticated guard receipt's AdmissionAttributionsJson instead.</param>
/// <param name="AcceptedAtGuardHighWater">Owning writer's exact guard high-water at that same transaction.</param><param name="AuthenticatedReceiptId">Independent exact durable transaction evidence.</param>
public sealed record DirectoryAtomicAppendOutcome(string OperationId, string RequestDigest, DirectoryAtomicAppendState State, long CommittedTargetRevision,
    long AcceptedAtAdmissionFenceOrdinal, long AcceptedAtGuardHighWater, string? AuthenticatedReceiptId);
