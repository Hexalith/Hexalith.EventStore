namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Actual loaded component instance, scopes, source/owner inventory and evidence associations.</summary>
/// <param name="Id">The required id.</param>
/// <param name="Name">The required name.</param>
/// <param name="ClusterId">The required cluster id.</param>
/// <param name="Namespace">The required namespace.</param>
/// <param name="ProviderId">The required provider id.</param>
/// <param name="Type">The required type.</param>
/// <param name="Version">The required version.</param>
/// <param name="LoadedIdentityDigest">The required loaded identity digest.</param>
/// <param name="ActorStateStore">The required actor state store.</param>
/// <param name="Stateful">The required stateful.</param>
/// <param name="Roles">The required roles.</param>
/// <param name="AppScopeIds">The required app scope ids.</param>
/// <param name="PhysicalTargetIds">The required physical target ids.</param>
/// <param name="CredentialReferenceIds">The required credential reference ids.</param>
/// <param name="AclDigest">The required acl digest.</param>
/// <param name="ResiliencyDigest">The required resiliency digest.</param>
/// <param name="DataMigrationDigest">The required data migration digest.</param>
/// <param name="SupportSources">The required support sources.</param>
/// <param name="AdvisorySources">The required advisory sources.</param>
/// <param name="CurrencyOwner">The required currency owner.</param>
/// <param name="RenewalOwners">The required renewal owners.</param>
/// <param name="EvidenceIds">The required evidence ids.</param>
public sealed record ProductionProfileComponent(
    string Id,
    string Name,
    string ClusterId,
    string Namespace,
    string ProviderId,
    string Type,
    string Version,
    string LoadedIdentityDigest,
    bool ActorStateStore,
    bool Stateful,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> AppScopeIds,
    IReadOnlyList<string> PhysicalTargetIds,
    IReadOnlyList<string> CredentialReferenceIds,
    string AclDigest,
    string ResiliencyDigest,
    string DataMigrationDigest,
    IReadOnlyList<string> SupportSources,
    IReadOnlyList<string> AdvisorySources,
    string CurrencyOwner,
    IReadOnlyList<string> RenewalOwners,
    IReadOnlyList<string> EvidenceIds);
