namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>The single compare-and-swap boundary for confirmation and task admission.</summary>
/// <param name="Scope">The authority bound to the storage key.</param>
/// <param name="Confirmation">The current or consumed artifact, if the intent requires one.</param>
/// <param name="Task">The committed task admission.</param>
internal sealed record DurableTaskAdmissionState(
    DurableTaskScope Scope,
    DurableTaskConfirmationState? Confirmation,
    CommittedDurableTaskAdmission? Task);
