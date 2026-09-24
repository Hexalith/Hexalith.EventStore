namespace Hexalith.EventStore.Client.Projections;

internal sealed record SharedProjectionChunk(
    string RootDigest,
    string SegmentDigest,
    byte[] Data,
    bool Tombstone);
