namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Exact scoped application instance and effective sidecar/security bindings.</summary>
/// <param name="Id">The required id.</param>
/// <param name="AppId">The required app id.</param>
/// <param name="ClusterId">The required cluster id.</param>
/// <param name="Namespace">The required namespace.</param>
/// <param name="ArtifactId">The required artifact id.</param>
/// <param name="ImageDigest">The required image digest.</param>
/// <param name="SidecarImageDigest">The required sidecar image digest.</param>
/// <param name="HostsActors">The required hosts actors.</param>
/// <param name="ActorTypes">The required actor types.</param>
/// <param name="ComponentIds">The required component ids.</param>
/// <param name="DaprApiTokenEnabled">The required dapr api token enabled.</param>
/// <param name="AppApiTokenEnabled">The required app api token enabled.</param>
/// <param name="DaprApiTokenCredentialId">The required dapr api token credential id.</param>
/// <param name="AppApiTokenCredentialId">The required app api token credential id.</param>
/// <param name="EffectiveConfigurationDigest">The required effective configuration digest.</param>
/// <param name="AclDigest">The required acl digest.</param>
/// <param name="ResiliencyDigest">The required resiliency digest.</param>
/// <param name="EvidenceIds">The required evidence ids.</param>
public sealed record ProductionProfileApplication(
    string Id,
    string AppId,
    string ClusterId,
    string Namespace,
    string ArtifactId,
    string ImageDigest,
    string SidecarImageDigest,
    bool HostsActors,
    IReadOnlyList<string> ActorTypes,
    IReadOnlyList<string> ComponentIds,
    bool DaprApiTokenEnabled,
    bool AppApiTokenEnabled,
    string DaprApiTokenCredentialId,
    string AppApiTokenCredentialId,
    string EffectiveConfigurationDigest,
    string AclDigest,
    string ResiliencyDigest,
    IReadOnlyList<string> EvidenceIds);
