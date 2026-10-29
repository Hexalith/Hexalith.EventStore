namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Exact protected snapshot entry path and complete target. Paths identify values within one original snapshot occurrence.</summary>
/// <param name="Path">Canonical RFC6901 path to the protected value.</param><param name="Target">Complete independently enrolled interaction root.</param>
public sealed record SnapshotProtectionEntry(string Path, ProtectionTarget Target);
