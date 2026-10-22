// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 12.1-12.3.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Carries one pure local classification: the selected route and its stored metadata, or one bounded rejection.
/// </summary>
/// <param name="Route">The selected route, or <see cref="CompatibilityReadRoute.Rejected"/>.</param>
/// <param name="Metadata">The classified stored metadata for a selected route, or <see langword="null"/>.</param>
/// <param name="UnreadableReason">The bounded rejection reason, or <see langword="null"/>.</param>
/// <param name="ProtectedSnapshot">The validated v2 snapshot carrier for a shared v2 snapshot route.</param>
internal sealed record CompatibilityClassification(
    CompatibilityReadRoute Route,
    EventStorePayloadProtectionMetadata? Metadata,
    UnreadableProtectedDataReason? UnreadableReason,
    ProtectedSnapshotPayloadV2? ProtectedSnapshot = null)
{
    /// <summary>Gets a value indicating whether classification rejected the record locally.</summary>
    internal bool IsRejected => UnreadableReason is not null;

    /// <summary>Creates a local rejection.</summary>
    internal static CompatibilityClassification Reject(UnreadableProtectedDataReason reason)
        => new(CompatibilityReadRoute.Rejected, null, reason);

    /// <summary>Creates one selected route.</summary>
    internal static CompatibilityClassification Select(
        CompatibilityReadRoute route,
        EventStorePayloadProtectionMetadata metadata,
        ProtectedSnapshotPayloadV2? protectedSnapshot = null)
        => new(route, metadata, null, protectedSnapshot);

    /// <inheritdoc/>
    public override string ToString() => nameof(CompatibilityClassification);
}
