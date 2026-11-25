using System.Text.Json.Serialization;

namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>The single compare-and-swap boundary for confirmation and task admission.</summary>
/// <param name="Scope">The authority bound to the storage key.</param>
/// <param name="Confirmation">The current or consumed artifact, if the intent requires one.</param>
/// <param name="Task">The committed task admission.</param>
/// <param name="Execution">The fenced execution state, absent on an admission that has never been leased.</param>
internal sealed record DurableTaskAdmissionState(
    DurableTaskScope Scope,
    DurableTaskConfirmationState? Confirmation,
    CommittedDurableTaskAdmission? Task,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DurableTaskExecutionState? Execution = null);
