namespace Hexalith.EventStore.Contracts.Replay;

/// <summary>Provides one replay page with an opaque, scoped private state handoff.</summary>
public interface IPagedReplayStateSession {
    /// <summary>Returns a detached copy of the admitted prior canonical state.</summary>
    ValueTask<ReadOnlyMemory<byte>> ReadPriorAsync(byte[] handle, CancellationToken cancellationToken);

    /// <summary>Writes one privately owned successor and returns only its opaque handle.</summary>
    ValueTask<byte[]> WriteSuccessorAsync(
        byte[] priorHandle,
        ReadOnlyMemory<byte> canonicalState,
        string serializerId,
        CancellationToken cancellationToken);

    /// <summary>Records one canonical post-Apply state for an opted-in timeline.</summary>
    void AppendTimelineEntry(long sequence, ReadOnlySpan<byte> canonicalPostApplyState);
}
