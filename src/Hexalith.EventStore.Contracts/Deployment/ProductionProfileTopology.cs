namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Pinned management/workload topology and access/backup/writer contracts.</summary>
/// <param name="DeploymentMode">The required deployment mode.</param>
/// <param name="ManagementClusterId">The required management cluster id.</param>
/// <param name="WorkloadClusterId">The required workload cluster id.</param>
/// <param name="ManagementProviderId">The required management provider id.</param>
/// <param name="WorkloadProviderId">The required workload provider id.</param>
/// <param name="RancherProviderId">The required rancher provider id.</param>
/// <param name="RancherLicense">The required rancher license.</param>
/// <param name="Capacity">The required capacity.</param>
/// <param name="PrivateAccessContractDigest">The required private access contract digest.</param>
/// <param name="AuthenticationContractDigest">The required authentication contract digest.</param>
/// <param name="AgentPermissionsContractDigest">The required agent permissions contract digest.</param>
/// <param name="BackupComponentIds">The required backup component ids.</param>
/// <param name="NativeRecoveryAccessContractDigest">The required native recovery access contract digest.</param>
/// <param name="DeploymentWriterBoundaryDigest">The required deployment writer boundary digest.</param>
public sealed record ProductionProfileTopology(
    string DeploymentMode,
    string ManagementClusterId,
    string WorkloadClusterId,
    string ManagementProviderId,
    string WorkloadProviderId,
    string RancherProviderId,
    string RancherLicense,
    string Capacity,
    string PrivateAccessContractDigest,
    string AuthenticationContractDigest,
    string AgentPermissionsContractDigest,
    IReadOnlyList<string> BackupComponentIds,
    string NativeRecoveryAccessContractDigest,
    string DeploymentWriterBoundaryDigest);
