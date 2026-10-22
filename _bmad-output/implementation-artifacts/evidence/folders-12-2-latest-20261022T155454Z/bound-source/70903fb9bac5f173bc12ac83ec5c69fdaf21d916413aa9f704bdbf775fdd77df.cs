namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Versioned exact shared transaction cell; content bytes must already be protected by their owning EventStore writer.</summary>
/// <param name="TenantId">Exact owner tenant.</param><param name="InstallationId">Immutable installed writer/source basis.</param>
/// <param name="CellId">Exact owner-supplied bounded cell identity within the tenant installation.</param>
/// <param name="Revision">Monotonic cell revision.</param><param name="Value">Owned serialized protected source or content-free guard state.</param>
public sealed record GuardedStateCell(string TenantId, string InstallationId, string CellId, long Revision, byte[] Value);
