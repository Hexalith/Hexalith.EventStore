namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Complete ordered restore posture and required logical readback evidence.</summary>
/// <param name="AcceptedPostureDigest">The required accepted posture digest.</param>
/// <param name="ConsistencyPoint">The required consistency point.</param>
/// <param name="BackupMethod">The required backup method.</param>
/// <param name="Units">The required units.</param>
/// <param name="Order">The required order.</param>
/// <param name="ForbiddenIndependentRestores">The required forbidden independent restores.</param>
/// <param name="EvidenceIds">The required evidence ids.</param>
public sealed record ProductionProfileRestore(
    string AcceptedPostureDigest,
    string ConsistencyPoint,
    string BackupMethod,
    IReadOnlyList<ProductionProfileRestoreUnit> Units,
    IReadOnlyList<string> Order,
    IReadOnlyList<string> ForbiddenIndependentRestores,
    IReadOnlyList<string> EvidenceIds);
