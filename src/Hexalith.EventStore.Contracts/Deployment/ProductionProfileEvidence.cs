namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Unverified evidence reference with owner, qualification/source identity and coverage associations.</summary>
/// <param name="Id">The required id.</param>
/// <param name="Owner">The required owner.</param>
/// <param name="QualificationId">The required qualification id.</param>
/// <param name="SourceIdentity">The required source identity.</param>
/// <param name="SubjectDigests">The required subject digests.</param>
/// <param name="AppIds">The required app ids.</param>
/// <param name="ComponentIds">The required component ids.</param>
/// <param name="PhysicalTargetIds">The required physical target ids.</param>
/// <param name="Coverage">The required coverage.</param>
public sealed record ProductionProfileEvidence(
    string Id,
    string Owner,
    string QualificationId,
    string SourceIdentity,
    IReadOnlyList<string> SubjectDigests,
    IReadOnlyList<string> AppIds,
    IReadOnlyList<string> ComponentIds,
    IReadOnlyList<string> PhysicalTargetIds,
    IReadOnlyList<string> Coverage);
