namespace Hexalith.EventStore.DomainService.Content;

/// <summary>Current authorization resource for tenant/task-scoped upload qualification.</summary>
/// <param name="Scope">Owning scope.</param>
/// <param name="TaskId">Exact uploading task.</param>
/// <param name="ReceiptId">Opaque upload identifier.</param>
public sealed record ContentUploadVerification(ContentScope Scope, string? TaskId, string ReceiptId);
