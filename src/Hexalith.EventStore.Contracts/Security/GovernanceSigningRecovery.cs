namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Current independently authenticated healthy signing successor basis and exact irreversible original no-issue outcome.</summary>
/// <param name="Payload">Original complete signed payload.</param><param name="SigningRequestId">Original canonical signing identity.</param>
/// <param name="DetachedJwsDigest">Original public artifact digest.</param><param name="NoIssueReceiptId">Exact original persisted terminal receipt.</param>
/// <param name="CurrentGuardRevision">Current logical guard compare.</param><param name="CurrentHealthyKeyVersion">Independently qualified current healthy per-tenant key.</param>
/// <param name="AuthorityRevision">Independent current nonrollback authority.</param><param name="ObservedAt">Current observation.</param><param name="ValidUntil">Exclusive authority lease.</param>
public sealed record GovernanceSigningRecovery(DeletionBatchCapabilityV1 Payload, string SigningRequestId, string DetachedJwsDigest, string NoIssueReceiptId,
    long CurrentGuardRevision, string CurrentHealthyKeyVersion, string AuthorityRevision, DateTimeOffset ObservedAt, DateTimeOffset ValidUntil);
