namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Production workload-assertion issuer and bootstrap/client references.</summary>
/// <param name="Id">The required id.</param>
/// <param name="ComponentId">The required component id.</param>
/// <param name="ContractDigest">The required contract digest.</param>
/// <param name="AssertionConfigurationDigest">The required assertion configuration digest.</param>
/// <param name="BootstrapCredentialReferenceId">The required bootstrap credential reference id.</param>
/// <param name="ClientCredentialReferenceIds">The required client credential reference ids.</param>
/// <param name="EvidenceIds">The required evidence ids.</param>
public sealed record ProductionProfileIssuer(
    string Id,
    string ComponentId,
    string ContractDigest,
    string AssertionConfigurationDigest,
    string BootstrapCredentialReferenceId,
    IReadOnlyList<string> ClientCredentialReferenceIds,
    IReadOnlyList<string> EvidenceIds);
