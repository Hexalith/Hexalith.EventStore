namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Invariant requirements bound into the infrastructure digest.</summary>
/// <param name="Id">The required id.</param>
/// <param name="RuntimeMinor">The required runtime minor.</param>
/// <param name="MinimumRuntimeVersion">The required minimum runtime version.</param>
/// <param name="SidecarMode">The required sidecar mode.</param>
/// <param name="ActorStateType">The required actor state type.</param>
/// <param name="ActorStateVersion">The required actor state version.</param>
/// <param name="ActorStateName">The required actor state name.</param>
/// <param name="Oq8Profile">The required oq8 profile.</param>
/// <param name="ScopeAclShapeDigest">The required scope acl shape digest.</param>
/// <param name="ResiliencyShapeDigest">The required resiliency shape digest.</param>
/// <param name="AppendSemantics">The required append semantics.</param>
/// <param name="SecretContractDigest">The required secret contract digest.</param>
/// <param name="OpenBaoServerFloor">The required open bao server floor.</param>
/// <param name="IssuerContractDigest">The required issuer contract digest.</param>
/// <param name="SchedulerContractDigest">The required scheduler contract digest.</param>
/// <param name="PlacementContractDigest">The required placement contract digest.</param>
/// <param name="RestoreContractDigest">The required restore contract digest.</param>
/// <param name="ExcludedProfiles">The required excluded profiles.</param>
public sealed record ProductionProfileTemplate(
    string Id,
    string RuntimeMinor,
    string MinimumRuntimeVersion,
    string SidecarMode,
    string ActorStateType,
    string ActorStateVersion,
    string ActorStateName,
    string Oq8Profile,
    string ScopeAclShapeDigest,
    string ResiliencyShapeDigest,
    IReadOnlyList<string> AppendSemantics,
    string SecretContractDigest,
    string OpenBaoServerFloor,
    string IssuerContractDigest,
    string SchedulerContractDigest,
    string PlacementContractDigest,
    string RestoreContractDigest,
    IReadOnlyList<string> ExcludedProfiles);
