namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>The current outcome of an admission attempt.</summary>
/// <param name="Outcome">The admission result class.</param>
/// <param name="Task">The committed task admission, present only on successful resolution.</param>
public sealed record DurableTaskAdmissionResult(DurableTaskAdmissionOutcome Outcome, CommittedDurableTaskAdmission? Task = null);
