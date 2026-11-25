namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>Authoritative proof that the owner has committed a terminal outcome for the step.</summary>
/// <param name="Step">The original scoped step the owner committed.</param>
/// <param name="ReceiptSha256">The lowercase SHA-256 digest of the owner's retained receipt evidence.</param>
public sealed record DurableTaskOwnerReceipt(DurableTaskStep Step, string ReceiptSha256);
