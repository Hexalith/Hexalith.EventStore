namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>Permits an authenticated owner workload to look up one registered numbered step.</summary>
/// <param name="Workload">The exact authenticated owner workload.</param>
/// <param name="AdmissionOperation">The exact, versioned Durable Task admission operation.</param>
/// <param name="StepNumber">The one-based number of the registered owner step.</param>
/// <param name="TargetDomain">The exact domain signed in the workload assertion.</param>
/// <param name="DeriveTargetId">Derives the concrete target from the committed admission and canonical step key.
/// The host supplies this deterministic function using the owner's identity rules.</param>
public sealed record DurableTaskStepLookupGrant(
    string Workload,
    string AdmissionOperation,
    int StepNumber,
    string TargetDomain,
    Func<CommittedDurableTaskAdmission, string, string> DeriveTargetId);
