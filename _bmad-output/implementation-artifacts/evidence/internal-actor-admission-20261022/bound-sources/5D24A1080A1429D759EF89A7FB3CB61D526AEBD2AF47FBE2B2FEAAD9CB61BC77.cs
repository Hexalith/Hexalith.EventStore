namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Carries one whole-state <c>json+pdenc-v2</c> protected snapshot in durable storage.
/// Implements Story 8.1 sections 6 and 9 under normative digest
/// <c>de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e</c>.
/// </summary>
/// <param name="Format">The exact serialization format, <c>json+pdenc-v2</c>.</param>
/// <param name="SnapshotTypeId">The registered stable snapshot type identifier.</param>
/// <param name="Envelope">The canonical unpadded base64url binary envelope.</param>
public sealed record ProtectedSnapshotPayloadV2(
    string Format,
    string SnapshotTypeId,
    string Envelope) {
    /// <summary>Returns a bounded diagnostic name without the durable protected envelope.</summary>
    /// <returns>The contract type name.</returns>
    public override string ToString() => nameof(ProtectedSnapshotPayloadV2);
}
