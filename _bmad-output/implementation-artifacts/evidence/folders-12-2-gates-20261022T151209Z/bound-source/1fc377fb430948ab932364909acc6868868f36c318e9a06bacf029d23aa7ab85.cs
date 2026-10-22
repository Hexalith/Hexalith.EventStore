using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Tests.Events;

/// <summary>Exercises bounded writer/scratch calls and records retained invocation handles for refusal assertions.</summary>
internal sealed class LeaseRecordingEventUpcaster(string behavior = "success", Action? afterWrite = null) : IEventUpcaster
{
    /// <summary>Gets the retained input handle, which must be invalid after invocation.</summary>
    internal IReadOnlyPayload? Input { get; private set; }

    /// <summary>Gets the retained output handle, which must never expose bytes or accept a later write.</summary>
    internal IBoundedPayloadWriter? Writer { get; private set; }

    /// <summary>Gets the retained allocator, which must refuse work after the invocation.</summary>
    internal IBoundedScratchAllocator? Scratch { get; private set; }

    /// <summary>Gets the exact forwarded cancellation token.</summary>
    internal CancellationToken ObservedToken { get; private set; }

    /// <inheritdoc/>
    public ValueTask<EventUpcastResult> UpcastAsync(IReadOnlyPayload input, IBoundedPayloadWriter output,
        IBoundedScratchAllocator scratch, CancellationToken cancellationToken)
    {
        Input = input;
        Writer = output;
        Scratch = scratch;
        ObservedToken = cancellationToken;
        scratch.WithScratch(input.Length, bytes =>
        {
            input.CopyTo(0, bytes);
            output.Write(bytes);
            bytes.Fill(99);
        }, cancellationToken);
        afterWrite?.Invoke();
        if (behavior == "throw")
        {
            throw new FormatException("Fixture failure after bounded output.");
        }

        if (behavior != "missing-complete")
        {
            output.Complete();
        }

        if (behavior == "swallowed-second-complete")
        {
            try
            {
                output.Complete();
            }
            catch (InvalidOperationException)
            {
                // The executor must still refuse the permanently violated writer.
            }
        }

        return ValueTask.FromResult(new EventUpcastResult("d", behavior == "wrong-identity" ? "wrong" : "evt", 2, "json"));
    }
}
