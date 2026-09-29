namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Durable quarantine evidence for a collision or malformed intent. It stores a digest of the evidence, never
/// the evidence itself, and remains until an operator disposes of it.
/// </summary>
/// <param name="EvidenceDigest">The Crockford Base32 SHA-256 digest of the quarantined evidence.</param>
/// <param name="ReasonCode">The bounded quarantine reason code.</param>
/// <param name="ReminderName">The affected reminder name, when the evidence produced one.</param>
/// <param name="RecordedAt">When the evidence was first quarantined.</param>
internal sealed record ReminderQuarantineRecord(
    string EvidenceDigest,
    string ReasonCode,
    string? ReminderName,
    DateTimeOffset RecordedAt);
