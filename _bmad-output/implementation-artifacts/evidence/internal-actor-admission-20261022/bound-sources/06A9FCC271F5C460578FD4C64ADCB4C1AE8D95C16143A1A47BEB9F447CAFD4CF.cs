namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Result returned by typed snapshot protection methods. Snapshot state and its protection metadata
/// travel together so callers never inspect protected content without the matching state record.
/// </summary>
/// <param name="State">The (possibly protected) snapshot state object.</param>
/// <param name="Metadata">The protection metadata describing how <paramref name="State"/> was produced.</param>
public sealed record SnapshotProtectionResult(object State, EventStorePayloadProtectionMetadata Metadata);
