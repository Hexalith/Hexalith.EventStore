namespace Hexalith.EventStore.Server.Tests.GlobalPositions;

/// <summary>
/// An already-decoded descriptor used only for local comparison experiments.
/// This is not a public metadata or wire-format implementation.
/// </summary>
/// <param name="MetadataVersion">The decoded metadata version.</param>
/// <param name="Scalar">The legacy scalar, or zero for a shard-local descriptor.</param>
/// <param name="Shard">The exact shard for v2, or the default value for v1.</param>
/// <param name="Generation">The v2 generation.</param>
/// <param name="Counter">The v2 counter.</param>
/// <param name="CanonicalizationVersion">The decoded shard canonicalization version.</param>
internal sealed record QualificationPosition(
    int MetadataVersion,
    long Scalar,
    QualificationShard Shard = default,
    long Generation = 0,
    long Counter = 0,
    int CanonicalizationVersion = 1)
{
    /// <summary>Gets the decoded scheme tag, independently of its outer metadata version.</summary>
    internal string Scheme { get; init; } = MetadataVersion == 1 ? "global-v1" : "tenant-domain-v2";
}
