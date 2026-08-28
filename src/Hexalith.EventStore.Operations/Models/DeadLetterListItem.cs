namespace Hexalith.EventStore.Operations.Models;

/// <summary>
/// Represents a redacted retained item returned by the actor.
/// </summary>
/// <param name="Identity">The safe identity.</param>
/// <param name="CapturedAtUtc">The capture time.</param>
/// <param name="ReplayAttempts">The replay attempt count.</param>
/// <param name="State">The durable state.</param>
/// <param name="LastReasonCode">The bounded last reason.</param>
public sealed record DeadLetterListItem(
    DeadLetterSafeIdentity Identity,
    DateTimeOffset CapturedAtUtc,
    int ReplayAttempts,
    DeadLetterReplayState State,
    string? LastReasonCode);
