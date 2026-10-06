using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Tests.Events;

/// <summary>Holds a hop across an await so a concurrent observation can invalidate its result.</summary>
internal sealed class SuspendedEventUpcaster : IEventUpcaster
{
    /// <summary>Signals that invocation has started.</summary>
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Controls the test-owned asynchronous continuation.</summary>
    internal TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the input facade for lifetime checks.</summary>
    internal IReadOnlyPayload? Input { get; private set; }

    /// <inheritdoc/>
    public async ValueTask<EventUpcastResult> UpcastAsync(IReadOnlyPayload input, IBoundedPayloadWriter output,
        IBoundedScratchAllocator scratch, CancellationToken cancellationToken)
    {
        Input = input;
        Entered.SetResult();
        await Resume.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        scratch.WithScratch(input.Length, bytes =>
        {
            input.CopyTo(0, bytes);
            output.Write(bytes);
        }, cancellationToken);
        output.Complete();
        return new EventUpcastResult("d", "evt", 2, "json");
    }
}
