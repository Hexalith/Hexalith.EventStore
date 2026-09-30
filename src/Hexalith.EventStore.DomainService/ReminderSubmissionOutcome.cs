using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Reminders;

namespace Hexalith.EventStore.DomainService;

/// <summary>The classified result of one reminder submission attempt.</summary>
/// <param name="Disposition">The audited disposition.</param>
/// <param name="ReasonCode">The bounded reason code.</param>
/// <param name="EffectId">The deterministic effect identifier, when a submission was built.</param>
/// <param name="Receipt">The durable target receipt, when one was returned.</param>
internal sealed record ReminderSubmissionOutcome(
    ReminderDisposition Disposition,
    string ReasonCode,
    string? EffectId,
    TrustedEffectResult? Receipt);
