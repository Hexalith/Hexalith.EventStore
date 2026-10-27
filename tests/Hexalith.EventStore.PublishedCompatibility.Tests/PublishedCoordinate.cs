namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Separately records tag and archive provenance without inferring one from the other.</summary>
internal sealed record PublishedCoordinate(string Version, string Tag, string TagSource, string TagBuilds,
    IReadOnlyDictionary<string, string> ArchiveSources, IReadOnlyDictionary<string, string> ArchiveHashes);
