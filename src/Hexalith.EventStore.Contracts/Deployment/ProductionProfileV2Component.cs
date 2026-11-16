namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>An actual complete infrastructure or Dapr instance; no namespace or configuration is fabricated.</summary>
/// <param name="Id">The independently inventoried instance identity.</param>
/// <param name="Name">The actual instance name.</param>
/// <param name="Kind">The closed instance kind.</param>
/// <param name="Scope">The actual infrastructure scope.</param>
/// <param name="ProviderId">The independently pinned provider.</param>
/// <param name="SoftwareVersion">The exact constituent software version matching its provider pin.</param>
/// <param name="LoadedIdentityDigest">The observed loaded instance identity.</param>
/// <param name="BundleParentId">The containing distribution instance, or null for a standalone instance.</param>
/// <param name="Dapr">Required exactly for dapr-component instances; otherwise null.</param>
/// <param name="Stateful">Whether this instance requires state restoration.</param>
/// <param name="Roles">The covered roles.</param>
/// <param name="PhysicalTargetIds">The physical target identities.</param>
/// <param name="CredentialReferenceIds">The reference-only credential associations.</param>
/// <param name="SupportSources">The support sources.</param>
/// <param name="AdvisorySources">The advisory sources.</param>
/// <param name="CurrencyOwner">The currency owner.</param>
/// <param name="RenewalOwners">The renewal owners.</param>
/// <param name="EvidenceIds">The unverified evidence references.</param>
public sealed record ProductionProfileV2Component(
    string Id,
    string Name,
    string Kind,
    ProductionProfileV2Scope Scope,
    string ProviderId,
    string SoftwareVersion,
    string LoadedIdentityDigest,
    string? BundleParentId,
    ProductionProfileV2Dapr? Dapr,
    bool Stateful,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> PhysicalTargetIds,
    IReadOnlyList<string> CredentialReferenceIds,
    IReadOnlyList<string> SupportSources,
    IReadOnlyList<string> AdvisorySources,
    string CurrencyOwner,
    IReadOnlyList<string> RenewalOwners,
    IReadOnlyList<string> EvidenceIds);
