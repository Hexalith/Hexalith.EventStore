namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>The v1 request to verify one committed Durable Task step for its concrete owner target.</summary>
/// <param name="TaskId">The admitted task identifier.</param>
/// <param name="StepKey">The canonical numbered key of the owner step.</param>
/// <param name="AdmissionOperation">The versioned operation of the admitted task.</param>
/// <param name="RequestSha256">The lowercase SHA-256 digest of the normalized admitted request.</param>
/// <param name="TargetDomain">The exact target domain bound in the workload assertion.</param>
/// <param name="TargetId">The intended concrete target identifier.</param>
/// <param name="Fence">The positive lease generation presented to the owner.</param>
public sealed record DurableTaskStepLookupRequest(
    string TaskId,
    string StepKey,
    string AdmissionOperation,
    string RequestSha256,
    string TargetDomain,
    string TargetId,
    long Fence);
