namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Exact immutable joint guard/source outcome persisted in the same transaction; never inferred from a save acknowledgement.</summary>
/// <param name="TenantId">Exact tenant.</param><param name="InstallationId">Original installed source/writer basis.</param>
/// <param name="OperationId">Original operation.</param><param name="RequestDigest">Exact detached original commit digest.</param>
/// <param name="GuardRevision">Actual committed guard revision, separate from any intended compare.</param><param name="ReceiptId">Stable content-free exact receipt.</param>
public sealed record GuardedStateCommitReceipt(string TenantId, string InstallationId, string OperationId, string RequestDigest, long GuardRevision, string ReceiptId)
{
    /// <summary>Original exact logical operation digest; empty on legacy primitive callers.</summary>
    public string LogicalIntentDigest { get; init; } = "";
    /// <summary>Detached content-free typed original outcome; it never establishes source authority independently.</summary>
    public byte[] Outcome { get; init; } = [];
    /// <summary>Actual logical guard high water; backend GuardRevision advances for every transaction including a no-effect outcome.</summary>
    public long LogicalGuardHighWater { get; init; }
}
