namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Exact immutable release bindings and claimed qualification references.</summary>
/// <param name="Id">The required id.</param>
/// <param name="TemplateId">The required template id.</param>
/// <param name="Artifacts">The required artifacts.</param>
/// <param name="ComposedHostDigest">The required composed host digest.</param>
/// <param name="ExtensionDigests">The required extension digests.</param>
/// <param name="HelmPackageDigest">The required helm package digest.</param>
/// <param name="CatalogContentDigest">The required catalog content digest.</param>
/// <param name="McpCliPackageDigest">The required mcp cli package digest.</param>
/// <param name="QualifiedInfrastructureSetIds">The required qualified infrastructure set ids.</param>
/// <param name="RuntimeVersion">The required runtime version.</param>
/// <param name="RuntimeImageDigest">The required runtime image digest.</param>
/// <param name="SdkVersion">The required sdk version.</param>
/// <param name="CliVersion">The required cli version.</param>
/// <param name="CliCompatibilityEvidenceId">The required cli compatibility evidence id.</param>
/// <param name="SidecarPatchVersion">The required sidecar patch version.</param>
/// <param name="RealmContractDigest">The required realm contract digest.</param>
/// <param name="RecoveryContractDigest">The required recovery contract digest.</param>
/// <param name="CheckSuiteContractDigest">The required check suite contract digest.</param>
/// <param name="EvidenceIds">The required evidence ids.</param>
public sealed record ProductionProfileRelease(
    string Id,
    string TemplateId,
    IReadOnlyList<ProductionProfileArtifact> Artifacts,
    string ComposedHostDigest,
    IReadOnlyList<string> ExtensionDigests,
    string HelmPackageDigest,
    string CatalogContentDigest,
    string McpCliPackageDigest,
    IReadOnlyList<string> QualifiedInfrastructureSetIds,
    string RuntimeVersion,
    string RuntimeImageDigest,
    string SdkVersion,
    string CliVersion,
    string CliCompatibilityEvidenceId,
    string SidecarPatchVersion,
    string RealmContractDigest,
    string RecoveryContractDigest,
    string CheckSuiteContractDigest,
    IReadOnlyList<string> EvidenceIds);
