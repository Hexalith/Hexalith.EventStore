namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Binds one physically independent executable to its exact dependency mode and inputs.</summary>
/// <param name="Role">The consumer role.</param>
/// <param name="Version">The selected package or source-tag coordinate.</param>
/// <param name="Configuration">The actual build configuration.</param>
/// <param name="Project">The isolated consumer project.</param>
/// <param name="Assembly">The executable assembly path.</param>
/// <param name="Cache">The invocation-specific NuGet cache.</param>
/// <param name="AssetsHash">The resolved graph hash.</param>
/// <param name="LockHash">The exact lock-file hash.</param>
/// <param name="Source">The exact source revision, or null for package consumption.</param>
/// <param name="ArchiveSources">The independently observed package source identities.</param>
/// <param name="ResolvedVersions">The versions resolved by this consumer graph.</param>
/// <param name="ProjectHash">The isolated project hash.</param>
/// <param name="VerifiedAssemblyHashes">The DLL hashes from independently downloaded and signature-verified nupkg entries.</param>
/// <param name="SdkPolicyHash">The exact global.json policy copied into the isolated build root.</param>
/// <param name="SelectedSdk">The SDK selected by dotnet in that root before restore and build.</param>
/// <param name="SdkPolicyOrigin">The owning repository or tagged-source policy path.</param>
internal sealed record ConsumerBuild(string Role, string Version, string Configuration, string Project,
    string Assembly, string Cache, string AssetsHash, string LockHash, string? Source,
    IReadOnlyDictionary<string, string>? ArchiveSources = null,
    IReadOnlyDictionary<string, string>? ResolvedVersions = null,
    string? ProjectHash = null,
    IReadOnlyDictionary<string, string>? VerifiedAssemblyHashes = null,
    string? SdkPolicyHash = null, string? SelectedSdk = null, string? SdkPolicyOrigin = null);
