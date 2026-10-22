namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Content-free joint transaction result; only Committed carries the exact original durable receipt.</summary>
/// <param name="Status">Closed observation.</param><param name="Receipt">Original exact durable receipt, when confirmed.</param>
public sealed record GuardedStateCommitResult(GuardedStateCommitStatus Status, GuardedStateCommitReceipt? Receipt = null);
