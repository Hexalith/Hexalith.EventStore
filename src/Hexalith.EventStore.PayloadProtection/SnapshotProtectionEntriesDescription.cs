using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Content-free complete snapshot geometry for independent source enrollment before the engine may issue any entry root.</summary>
/// <param name="ContainerDigest">Digest of the public technical skeleton.</param><param name="Entries">Exact original ordered value paths and complete targets.</param>
public sealed record SnapshotProtectionEntriesDescription(string ContainerDigest, IReadOnlyList<SnapshotProtectionEntry> Entries);
