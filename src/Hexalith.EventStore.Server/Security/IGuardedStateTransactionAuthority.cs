using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Independent private writer installation, complete source migration, joint backend CAS and nonrollback restore authority; no default implementation is supplied.</summary>
public interface IGuardedStateTransactionAuthority
{
    /// <summary>Authorizes an exact installed cell read for the restricted coordinator; omission denies it.</summary>
    Task<bool> AuthorizeReadAsync(GuardedStateTransactionTarget target, string cellId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
    /// <summary>Authorizes private exact original operation lookup by immutable logical intent, never a new effect.</summary>
    Task<bool> AuthorizeLookupAsync(GuardedStateTransactionTarget target, string operationId, string logicalIntentDigest,
        CancellationToken cancellationToken = default) => Task.FromResult(false);
    /// <summary>Authenticates existing backend atomic multi-cell ETag behavior, tenant partition, preinstalled single guard, migrated source routing and complete legacy writer revocation.</summary>
    Task<GuardedStateTransactionTarget?> GetCurrentAsync(string tenantId, CancellationToken cancellationToken = default);
    /// <summary>Authenticates exact current private operation, source/cell allowlist and already verified domain/epoch/scope/guard transition; credentials alone cannot self-authorize a transition.</summary>
    Task<bool> AuthorizeAsync(GuardedStateTransactionTarget target, GuardedStateCommitRequest request, string requestDigest,
        string operation, CancellationToken cancellationToken = default);
    /// <summary>Authenticates original installed cell revision/digest against complete independent source and restore provenance. Missing old state cannot become fresh absence.</summary>
    Task<bool> ValidateCellAsync(GuardedStateTransactionTarget target, GuardedStateCell? cell, string cellId,
        CancellationToken cancellationToken = default);
    /// <summary>Authenticates an exact original receipt and its immutable joint source/guard/outcome provenance after outage/restore.</summary>
    Task<bool> ValidateReceiptAsync(GuardedStateTransactionTarget target, GuardedStateCommitReceipt receipt, CancellationToken cancellationToken = default);
}
