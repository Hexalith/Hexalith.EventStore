using Hexalith.EventStore.Contracts.Effects;

namespace Hexalith.EventStore.Client.Reminders;

/// <summary>The bindings a reminder's workload delegation must cover.</summary>
/// <param name="Submission">The exact trusted-effect submission, including its identity tuple and command.</param>
/// <param name="Workload">The workload that submits the effect.</param>
/// <param name="Purpose">The named delegated purpose configured for the reminder kind.</param>
/// <param name="CausationId">The stable logical-effect causation identifier, <c>wrk-&lt;EffectId&gt;</c>, shared with submission.</param>
public sealed record ReminderDelegationRequest(
    TrustedEffectSubmission Submission,
    string Workload,
    string Purpose,
    string CausationId);
