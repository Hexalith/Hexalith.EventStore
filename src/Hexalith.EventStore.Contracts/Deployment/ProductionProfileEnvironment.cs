namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Environment-current value-free bindings excluded from the infrastructure digest.</summary>
/// <param name="Id">The required id.</param>
/// <param name="ClusterIds">The required cluster ids.</param>
/// <param name="Endpoints">The required endpoints.</param>
/// <param name="PhysicalTargets">The required physical targets.</param>
/// <param name="Credentials">The required credentials.</param>
/// <param name="KeyGenerations">The required key generations.</param>
/// <param name="AppliedRealmDigest">The required applied realm digest.</param>
/// <param name="SecretContractDigest">The required secret contract digest.</param>
/// <param name="OpenBaoServerVersion">The required open bao server version.</param>
/// <param name="OpenBaoComponentId">The required open bao component id.</param>
/// <param name="BrokerComponentId">The required broker component id.</param>
/// <param name="BrokerContractDigest">The required broker contract digest.</param>
/// <param name="BrokerEvidenceIds">The required broker evidence ids.</param>
/// <param name="SchedulerComponentIds">The required scheduler component ids.</param>
/// <param name="PlacementComponentIds">The required placement component ids.</param>
/// <param name="SchedulerConfigurationDigest">The required scheduler configuration digest.</param>
/// <param name="PlacementConfigurationDigest">The required placement configuration digest.</param>
/// <param name="SchedulerPlacementEvidenceIds">The required scheduler placement evidence ids.</param>
/// <param name="ControlPlaneDropAllCapabilities">The required control plane drop all capabilities.</param>
/// <param name="EffectiveConfigurationDigest">The required effective configuration digest.</param>
/// <param name="RenderedDeploymentDigest">The required rendered deployment digest.</param>
public sealed record ProductionProfileEnvironment(
    string Id,
    IReadOnlyList<string> ClusterIds,
    IReadOnlyList<ProductionProfileEndpoint> Endpoints,
    IReadOnlyList<ProductionProfilePhysicalTarget> PhysicalTargets,
    IReadOnlyList<ProductionProfileCredentialReference> Credentials,
    IReadOnlyList<string> KeyGenerations,
    string AppliedRealmDigest,
    string SecretContractDigest,
    string OpenBaoServerVersion,
    string OpenBaoComponentId,
    string BrokerComponentId,
    string BrokerContractDigest,
    IReadOnlyList<string> BrokerEvidenceIds,
    IReadOnlyList<string> SchedulerComponentIds,
    IReadOnlyList<string> PlacementComponentIds,
    string SchedulerConfigurationDigest,
    string PlacementConfigurationDigest,
    IReadOnlyList<string> SchedulerPlacementEvidenceIds,
    bool ControlPlaneDropAllCapabilities,
    string EffectiveConfigurationDigest,
    string RenderedDeploymentDigest);
