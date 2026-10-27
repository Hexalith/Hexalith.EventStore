namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Separately records tag and archive provenance without inferring one from the other.</summary>
/// <param name="Version">The immutable package version.</param>
/// <param name="Tag">The independently inspected source tag.</param>
/// <param name="TagSource">The tag source revision.</param>
/// <param name="TagBuilds">The Builds revision recorded by the tag.</param>
/// <param name="ArchiveSources">The independently observed archive source revisions.</param>
/// <param name="ArchiveHashes">The verified archive SHA-256 hashes.</param>
/// <param name="AssemblyHashes">The SDK DLL hashes read directly from signature-verified archive entries.</param>
internal sealed record PublishedCoordinate(string Version, string Tag, string TagSource, string TagBuilds,
    IReadOnlyDictionary<string, string> ArchiveSources, IReadOnlyDictionary<string, string> ArchiveHashes,
    IReadOnlyDictionary<string, string> AssemblyHashes);
