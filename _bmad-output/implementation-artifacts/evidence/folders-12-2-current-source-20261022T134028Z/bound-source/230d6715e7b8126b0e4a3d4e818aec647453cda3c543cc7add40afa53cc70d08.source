using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Injected guarded-cell installation. Missing installation denies read, lookup, commit, and receipt checks.</summary>
public interface IGuardedStateInstallation
{
    /// <summary>Reads the current installed target.</summary>
    /// <param name="tenantId">Exact tenant.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The current target, or null.</returns>
    Task<GuardedStateTransactionTarget?> GetCurrentAsync(string tenantId, CancellationToken cancellationToken);

    /// <summary>Authorizes a read of one current cell.</summary>
    /// <param name="target">Current target.</param>
    /// <param name="cellId">Cell id.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>True only for that installed cell.</returns>
    Task<bool> AuthorizeReadAsync(GuardedStateTransactionTarget target, string cellId, CancellationToken cancellationToken);

    /// <summary>Authorizes lookup of one original operation. Lookup starts no effect.</summary>
    /// <param name="target">Current target.</param>
    /// <param name="operationId">Original operation id.</param>
    /// <param name="logicalIntentDigest">Logical intent digest.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>True only for that original lookup.</returns>
    Task<bool> AuthorizeLookupAsync(GuardedStateTransactionTarget target, string operationId, string logicalIntentDigest, CancellationToken cancellationToken);

    /// <summary>Authorizes one commit against the current target.</summary>
    /// <param name="target">Current target.</param>
    /// <param name="request">Commit request.</param>
    /// <param name="requestDigest">Request digest.</param>
    /// <param name="operation">Operation name.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>True only for that exact commit.</returns>
    Task<bool> AuthorizeAsync(GuardedStateTransactionTarget target, GuardedStateCommitRequest request, string requestDigest, string operation, CancellationToken cancellationToken);

    /// <summary>Classifies one cell, including restored absence.</summary>
    /// <param name="target">Current target.</param>
    /// <param name="cellId">Cell id.</param>
    /// <param name="cell">Observed cell, or null when absent.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The installed history.</returns>
    Task<GuardedCellHistory> ReadCellAsync(GuardedStateTransactionTarget target, string cellId, GuardedStateCell? cell, CancellationToken cancellationToken);

    /// <summary>Validates one original receipt.</summary>
    /// <param name="target">Current target.</param>
    /// <param name="receipt">Receipt.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>True only for the exact installed receipt.</returns>
    Task<bool> ValidateReceiptAsync(GuardedStateTransactionTarget target, GuardedStateCommitReceipt receipt, CancellationToken cancellationToken);
}
