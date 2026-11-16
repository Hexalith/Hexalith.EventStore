namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Mandatory append/erasure tuple; claimed qualification is not authenticated.</summary>
/// <param name="Mode">The required mode.</param>
/// <param name="ImplementationId">The required implementation id.</param>
/// <param name="TransactionSemanticsDigest">The required transaction semantics digest.</param>
/// <param name="SdkVersion">The required sdk version.</param>
/// <param name="RuntimeVersion">The required runtime version.</param>
/// <param name="ComponentVersions">The required component versions.</param>
/// <param name="ActorOwnership">The required actor ownership.</param>
/// <param name="MandatoryGuard">The required mandatory guard.</param>
/// <param name="Semantics">The required semantics.</param>
/// <param name="Writers">The required writers.</param>
/// <param name="PlacementFailoverDigest">The required placement failover digest.</param>
/// <param name="AuthorizedErasureContractDigest">The required authorized erasure contract digest.</param>
/// <param name="EvidenceIds">The required evidence ids.</param>
public sealed record ProductionProfileProtection(
    string Mode,
    string ImplementationId,
    string TransactionSemanticsDigest,
    string SdkVersion,
    string RuntimeVersion,
    IReadOnlyList<ProductionProfileComponentVersion> ComponentVersions,
    string ActorOwnership,
    bool MandatoryGuard,
    IReadOnlyList<string> Semantics,
    IReadOnlyList<ProductionProfileWriter> Writers,
    string PlacementFailoverDigest,
    string AuthorizedErasureContractDigest,
    IReadOnlyList<string> EvidenceIds);
