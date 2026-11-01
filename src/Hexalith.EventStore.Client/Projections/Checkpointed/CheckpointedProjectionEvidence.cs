namespace Hexalith.EventStore.Client.Projections.Checkpointed;

/// <summary>
/// The delivered or authoritative identity of one event at one aggregate sequence.
/// </summary>
/// <param name="MessageId">The persisted event message identifier.</param>
/// <param name="EventType">The persisted event type name.</param>
/// <param name="PayloadFingerprint">The lowercase hexadecimal SHA-256 digest of the exact persisted payload bytes.</param>
public sealed record CheckpointedProjectionEvidence(
    string MessageId,
    string EventType,
    string PayloadFingerprint) {
    /// <summary>Gets a value indicating whether every identity component is present.</summary>
    public bool IsComplete
        => !string.IsNullOrWhiteSpace(MessageId)
        && !string.IsNullOrWhiteSpace(EventType)
        && !string.IsNullOrWhiteSpace(PayloadFingerprint);
}
