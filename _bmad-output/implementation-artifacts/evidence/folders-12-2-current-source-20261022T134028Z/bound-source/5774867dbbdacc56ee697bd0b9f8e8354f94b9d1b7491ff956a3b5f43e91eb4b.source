using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Transforms one retained event payload to its adjacent registered version.</summary>
public interface IEventUpcaster {
    /// <summary>Upcasts an immutable payload using only bounded output and scratch buffers.</summary>
    /// <param name="input">The privately owned immutable input view.</param>
    /// <param name="output">The bounded output writer.</param>
    /// <param name="scratch">The bounded scratch allocator.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>The declared identity and format of the output.</returns>
    ValueTask<EventUpcastResult> UpcastAsync(
        IReadOnlyPayload input,
        IBoundedPayloadWriter output,
        IBoundedScratchAllocator scratch,
        CancellationToken cancellationToken);
}
