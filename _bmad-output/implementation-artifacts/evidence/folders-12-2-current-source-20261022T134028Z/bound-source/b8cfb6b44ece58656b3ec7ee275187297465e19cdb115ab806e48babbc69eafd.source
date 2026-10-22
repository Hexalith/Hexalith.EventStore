using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Events;

/// <summary>
/// Story 22.7b — typed exception carrying a safe <see cref="UnreadableProtectedDataReason"/> and a
/// pipeline stage label when EventStore needs to surface unreadable-protected-data through the
/// existing infrastructure failure / dead-letter routing path. The exception message is fixed and
/// derives only from the stable wire reason code; it never contains payload bytes, snapshot state,
/// key material, provider exception text, or provider-private metadata.
/// </summary>
public sealed class ProtectedDataUnreadableException : InvalidOperationException {
    /// <summary>
    /// Initializes a new instance of the <see cref="ProtectedDataUnreadableException"/> class.
    /// </summary>
    /// <param name="reason">The unreadable reason category.</param>
    /// <param name="stage">Optional pipeline stage label (e.g. <c>"rehydrate"</c>, <c>"publish"</c>).</param>
    /// <param name="sequenceNumber">Optional event sequence number associated with the unreadable record.</param>
    public ProtectedDataUnreadableException(
        UnreadableProtectedDataReason reason,
        string? stage = null,
        long? sequenceNumber = null)
        : base(BuildSafeMessage(reason, stage, sequenceNumber)) {
        Reason = reason;
        Stage = stage;
        SequenceNumber = sequenceNumber;
    }

    /// <summary>Gets the unreadable reason category.</summary>
    public UnreadableProtectedDataReason Reason { get; }

    /// <summary>Gets the stable kebab-case reason code.</summary>
    public string ReasonCode => UnreadableProtectedDataReasonCodes.From(Reason);

    /// <summary>Gets the optional pipeline stage label.</summary>
    public string? Stage { get; }

    /// <summary>Gets the optional affected event sequence number.</summary>
    public long? SequenceNumber { get; }

    private static string BuildSafeMessage(UnreadableProtectedDataReason reason, string? stage, long? sequenceNumber) {
        // Message is intentionally fixed and built from the stable reason code only. Do not include
        // provider exception text, payload bytes, snapshot state, key material, or state-store keys.
        string code = UnreadableProtectedDataReasonCodes.From(reason);
        string stageLabel = string.IsNullOrWhiteSpace(stage) ? "unspecified" : stage;
        string seqLabel = sequenceNumber.HasValue ? sequenceNumber.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "n/a";
        return $"Protected data is unreadable. Stage={stageLabel}, Sequence={seqLabel}, ReasonCode={code}.";
    }
}
