namespace Hexalith.EventStore.Operations.Models;

/// <summary>
/// Requests one tenant-scoped action over retained message ids.
/// </summary>
/// <param name="TenantId">The authorized tenant scope.</param>
/// <param name="MessageIds">The message identifiers.</param>
public sealed record DeadLetterActionRequest(string TenantId, IReadOnlyList<string> MessageIds);
