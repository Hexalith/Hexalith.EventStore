namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Replica/freshness posture; no unproven multi-replica claim is accepted.</summary>
/// <param name="EventStoreAppId">The required event store app id.</param>
/// <param name="Replicas">The required replicas.</param>
/// <param name="MultiReplicaFreshnessClaim">The required multi replica freshness claim.</param>
/// <param name="PostureDigest">The required posture digest.</param>
/// <param name="EvidenceIds">The required evidence ids.</param>
public sealed record ProductionProfileFreshness(
    string EventStoreAppId,
    int Replicas,
    bool MultiReplicaFreshnessClaim,
    string PostureDigest,
    IReadOnlyList<string> EvidenceIds);
