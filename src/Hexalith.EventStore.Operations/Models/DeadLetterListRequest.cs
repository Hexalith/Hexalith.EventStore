namespace Hexalith.EventStore.Operations.Models;

/// <summary>
/// Requests a bounded tenant-scoped page from the drain actor.
/// </summary>
/// <param name="TenantId">The tenant scope, or null for the trusted admin-wide view.</param>
/// <param name="Count">The maximum page size.</param>
/// <param name="Offset">The zero-based continuation offset.</param>
public sealed record DeadLetterListRequest(string? TenantId, int Count, int Offset);
