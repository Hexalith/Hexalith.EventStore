using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>One immutable exact pending/committed/authorized operation in the atomically fenced finite cohort; no new operation may enter the bridge.</summary>
/// <param name="Owner">Authoritative original owner.</param><param name="OperationId">Exact original operation.</param><param name="Kind">Closed concrete writer kind.</param>
/// <param name="SourceConversationId">Immutable permit/first-event Conversation binding.</param><param name="PermitId">Original permit.</param>
/// <param name="EffectCapabilityId">Exact originally recorded effect capability.</param><param name="OwnerRevision">Original capability revision.</param>
/// <param name="OriginalState">Exactly Pending, Committed or Authorized.</param>
public sealed record DirectoryRepairCohortItem(AggregateIdentity Owner, string OperationId, DirectoryWriteKind Kind, string SourceConversationId,
    string PermitId, string EffectCapabilityId, long OwnerRevision, string OriginalState);
