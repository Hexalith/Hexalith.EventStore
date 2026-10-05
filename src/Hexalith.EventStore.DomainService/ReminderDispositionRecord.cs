using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Reminders;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// The latest durable audit disposition of one reminder subject. Witness release waits for this audit.
/// Independent <see cref="ReminderQuarantineRecord"/> evidence can be persisted when its audit fails and is
/// audited again on every convergence. An entry-quarantine transition whose audit fails retains the witness
/// as <see cref="ReminderEntryStatus.Retrying"/> with <c>audit-unavailable</c>.
/// </summary>
/// <param name="Tenant">The canonical tenant.</param>
/// <param name="ActorId">The <c>wra-</c> actor identifier.</param>
/// <param name="Subject">The reminder name, or the quarantine evidence digest.</param>
/// <param name="Disposition">The audited disposition.</param>
/// <param name="ReasonCode">The bounded reason code.</param>
/// <param name="EffectId">The deterministic effect identifier, when a submission was built.</param>
/// <param name="TargetDisposition">The target receipt disposition, when one was returned.</param>
/// <param name="Replayed">Whether the target returned a prior receipt.</param>
/// <param name="Attempts">The retry count for unresolved submission, admission, audit, or cancellation work at the time of this disposition.</param>
/// <param name="RecordedAt">When the disposition was recorded.</param>
internal sealed record ReminderDispositionRecord(
    string Tenant,
    string ActorId,
    string Subject,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] ReminderDisposition Disposition,
    string ReasonCode,
    string? EffectId,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] TrustedEffectDisposition? TargetDisposition,
    bool Replayed,
    int Attempts,
    DateTimeOffset RecordedAt);
