using System.Text.Json.Serialization;

namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>The committed admission and step state visible to the registered owner for the current fence.</summary>
/// <param name="TenantId">The committed Tenant.</param>
/// <param name="OriginalActorId">The committed actor whose current authority the owner must independently evaluate.</param>
/// <param name="AdmissionOperation">The committed versioned admission operation.</param>
/// <param name="RequestSha256">The committed normalized request digest.</param>
/// <param name="TaskId">The committed task identifier.</param>
/// <param name="StepKey">The canonical owner step key, which the owner uses to deduplicate the effect.</param>
/// <param name="Status">The committed lifecycle state.</param>
/// <param name="Receipt">The committed owner receipt identifier for this step, if available.</param>
/// <param name="AcceptsExecution">Whether the persisted task permits execution of a pending step under the presented fence.</param>
public sealed record DurableTaskStepLookupResult(
    string TenantId,
    string OriginalActorId,
    string AdmissionOperation,
    string RequestSha256,
    string TaskId,
    string StepKey,
    [property: JsonConverter(typeof(JsonStringEnumConverter<DurableTaskStatus>))] DurableTaskStatus Status,
    string? Receipt,
    bool AcceptsExecution);
