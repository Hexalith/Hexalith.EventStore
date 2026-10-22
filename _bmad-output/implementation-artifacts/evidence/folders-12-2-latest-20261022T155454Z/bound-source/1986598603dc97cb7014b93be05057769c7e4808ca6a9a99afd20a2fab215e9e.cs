using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Downserializes a current payload to one explicitly registered V1 alias.</summary>
[Obsolete("Legacy downserialization compatibility contract; no new downserialization API is provided.")]
public interface IV1Downserializer {
    /// <summary>Downserializes an immutable payload using bounded output and scratch buffers.</summary>
    /// <param name="input">The privately owned immutable input view.</param>
    /// <param name="output">The bounded output writer.</param>
    /// <param name="scratch">The bounded scratch allocator.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>The declared V1 alias identity and output format.</returns>
    ValueTask<V1DownserializeResult> DownserializeAsync(
        IReadOnlyPayload input,
        IBoundedPayloadWriter output,
        IBoundedScratchAllocator scratch,
        CancellationToken cancellationToken);
}
