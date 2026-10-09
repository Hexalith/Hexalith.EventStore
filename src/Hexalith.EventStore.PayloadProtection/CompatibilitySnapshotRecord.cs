// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 6.1, 12.3, and 13.
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Carries one stored snapshot exactly as persisted, before any compatibility decision (normative section 12.3).
/// </summary>
/// <param name="Identity">The authenticated aggregate identity of the storage scope.</param>
/// <param name="SnapshotSequence">The persisted snapshot sequence.</param>
/// <param name="State">
/// The stored state: the <see cref="System.Text.Json.JsonElement"/> read back from storage, or an in-process
/// <see cref="ProtectedSnapshotPayloadV2"/> carrier. Any other type is rejected as an invalid argument.
/// </param>
/// <param name="ProtectionMetadata">The stored snapshot metadata, or <see langword="null"/> for a legacy snapshot.</param>
internal sealed record CompatibilitySnapshotRecord(
    AggregateIdentity Identity,
    ulong SnapshotSequence,
    object State,
    EventStorePayloadProtectionMetadata? ProtectionMetadata)
{
    /// <inheritdoc/>
    public override string ToString() => nameof(CompatibilitySnapshotRecord);
}
