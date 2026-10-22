namespace Hexalith.EventStore.Client.Projections;

/// <summary>Seals immutable exact-byte recovery records without issuing create-only writes the store must reject.</summary>
/// <remarks>
/// Retained batch recovery re-seals participant proof and terminal fences from install resume, compaction, abort and
/// cleanup after an earlier step already wrote them. A rejected create-only write is not free on a DAPR sidecar: its
/// resiliency policy can retry the deterministic rejection and count it toward the state store circuit breaker, which
/// then fails every state operation of that sidecar. Reading first keeps the success path free of rejected writes.
/// </remarks>
internal static class ReadModelBatchImmutableRecord
{
    /// <summary>Returns whether <paramref name="key"/> durably holds exactly <paramref name="bytes"/>.</summary>
    /// <remarks>
    /// An existing identical record is accepted without a write and an existing divergent record is refused. Only an
    /// absent key is created; a lost creation race falls back to the same exact-bytes comparison.
    /// </remarks>
    /// <param name="accessor">The raw batch state accessor.</param>
    /// <param name="key">The immutable record key.</param>
    /// <param name="bytes">The exact canonical record bytes.</param>
    /// <param name="token">A cancellation token.</param>
    /// <returns><see langword="true"/> when the key holds exactly <paramref name="bytes"/>.</returns>
    internal static async Task<bool> SealAsync(IReadModelBatchStateAccessor accessor, string key, ReadOnlyMemory<byte> bytes, CancellationToken token)
    {
        RawStateEntry existing = await accessor.ReadAsync(key, token).ConfigureAwait(false);
        if (existing.Exists) return existing.Value.Span.SequenceEqual(bytes.Span);
        if (await accessor.TryWriteAsync(key, bytes, string.Empty, token).ConfigureAwait(false)) return true;
        RawStateEntry raced = await accessor.ReadAsync(key, token).ConfigureAwait(false);
        return raced.Exists && raced.Value.Span.SequenceEqual(bytes.Span);
    }
}
