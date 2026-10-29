namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Compatible mixed-root snapshot carrier. Technical skeleton bytes contain null protected values; every value is held only in its own V2 authenticated entry envelope.</summary>
/// <param name="Format">Exact json+pdenc-v2-entries format.</param><param name="SnapshotTypeId">Original registered snapshot type.</param>
/// <param name="SkeletonBytes">Content-free original technical skeleton with each protected value replaced by null.</param>
/// <param name="Entries">Complete original path/target manifest and independently sealed values.</param>
public sealed record ProtectedSnapshotEntriesV2(string Format, string SnapshotTypeId, byte[] SkeletonBytes, IReadOnlyList<ProtectedSnapshotEntryV2> Entries);
