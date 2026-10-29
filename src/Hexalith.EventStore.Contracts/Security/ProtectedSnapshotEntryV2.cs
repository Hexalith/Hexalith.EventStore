namespace Hexalith.EventStore.Contracts.Security;

/// <summary>One entry authenticated under its own original interaction root and snapshot sub-occurrence.</summary>
/// <param name="Path">Original protected value path.</param><param name="Target">Original complete target.</param><param name="KeyReference">Original sealed occurrence reference.</param>
/// <param name="Protected">Unchanged existing V2 snapshot envelope for the entry wrapper.</param>
public sealed record ProtectedSnapshotEntryV2(string Path, ProtectionTarget Target, string KeyReference, ProtectedSnapshotPayloadV2 Protected);
