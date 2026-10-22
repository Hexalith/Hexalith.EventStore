namespace Hexalith.EventStore.Client.Projections;

internal sealed record SharedProjectionChunkReference(
    string Purpose,
    string Identity,
    string Digest,
    int ByteLength,
    int ChunkCount);
