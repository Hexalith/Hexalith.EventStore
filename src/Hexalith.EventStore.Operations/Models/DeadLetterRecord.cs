namespace Hexalith.EventStore.Operations.Models;

/// <summary>
/// Represents a durably retained subscriber dead letter, including its original bytes.
/// </summary>
/// <param name="Identity">The safe identity extracted from the envelope.</param>
/// <param name="Topic">The source dead-letter topic.</param>
/// <param name="Body">The exact structured CloudEvent bytes.</param>
/// <param name="BodySha256">The lowercase SHA-256 body hash.</param>
/// <param name="CapturedAtUtc">The capture time.</param>
/// <param name="State">The durable replay state.</param>
/// <param name="ReplayAttempts">The number of replay delivery attempts.</param>
/// <param name="LastReasonCode">The last bounded outcome reason.</param>
public sealed record DeadLetterRecord(
    DeadLetterSafeIdentity Identity,
    string Topic,
    byte[] Body,
    string BodySha256,
    DateTimeOffset CapturedAtUtc,
    DeadLetterReplayState State,
    int ReplayAttempts,
    string? LastReasonCode)
{
    /// <summary>Gets the action currently associated with this record.</summary>
    public string? ActionOperationId { get; init; }

    /// <summary>Gets the content and scope fingerprint for a validated subscriber transfer.</summary>
    public string? DeliveryFingerprint { get; init; }

    /// <summary>Gets the persisted subscriber attempt evidence.</summary>
    public SubscriberFailureEvidence? DeliveryFailure { get; init; }
}
