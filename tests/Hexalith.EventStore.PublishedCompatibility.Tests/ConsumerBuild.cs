namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Binds one physically independent executable to its exact dependency mode and inputs.</summary>
internal sealed record ConsumerBuild(string Role, string Version, string Configuration, string Project,
    string Assembly, string Cache, string AssetsHash, string LockHash, string? Source,
    IReadOnlyDictionary<string, string>? ArchiveSources = null,
    IReadOnlyDictionary<string, string>? ResolvedVersions = null,
    string? ProjectHash = null);
