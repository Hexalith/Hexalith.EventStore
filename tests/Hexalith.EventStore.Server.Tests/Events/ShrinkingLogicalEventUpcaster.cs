using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Provides a bounded smaller current view to test readable-source page accounting.</summary>
internal sealed class ShrinkingLogicalEventUpcaster : IEventUpcaster
{
    /// <summary>Gets the number of domain transformations actually invoked.</summary>
    internal int Calls { get; private set; }

    /// <inheritdoc/>
    public ValueTask<EventUpcastResult> UpcastAsync(IReadOnlyPayload input, IBoundedPayloadWriter output,
        IBoundedScratchAllocator scratch, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        output.Write([1]);
        output.Complete();
        return ValueTask.FromResult(new EventUpcastResult("d", "evt", 2, "json"));
    }
}
