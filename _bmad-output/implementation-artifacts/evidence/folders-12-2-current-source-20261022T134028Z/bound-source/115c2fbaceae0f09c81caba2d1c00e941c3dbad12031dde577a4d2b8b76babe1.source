namespace Hexalith.EventStore.Client.Projections;

/// <summary>Durable append cursor for a reserved streamed shared-projection rebuild.</summary>
public sealed record SharedProjectionStreamingStageStatus(
    long Epoch,
    int CompletedPageCount,
    int ReservedPageCount,
    bool IsSealed,
    string? Fingerprint);
