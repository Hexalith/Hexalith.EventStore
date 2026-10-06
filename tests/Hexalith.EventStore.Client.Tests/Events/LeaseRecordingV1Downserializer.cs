using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Tests.Events;

/// <summary>Exercises exact alias output and retains only the supplied invocation facades.</summary>
/// <param name="behavior">The output contract control.</param>
/// <param name="afterWrite">The test-owned observation invoked after output writing.</param>
internal sealed class LeaseRecordingV1Downserializer(string behavior = "success", Action? afterWrite = null) : IV1Downserializer
{
    /// <summary>Gets the retained input handle.</summary>
    internal IReadOnlyPayload? Input { get; private set; }

    /// <summary>Gets the retained allocator handle.</summary>
    internal IBoundedScratchAllocator? Scratch { get; private set; }

    /// <inheritdoc/>
    public ValueTask<V1DownserializeResult> DownserializeAsync(IReadOnlyPayload input, IBoundedPayloadWriter output,
        IBoundedScratchAllocator scratch, CancellationToken cancellationToken)
    {
        Input = input;
        Scratch = scratch;
        scratch.WithScratch(input.Length, bytes =>
        {
            input.CopyTo(0, bytes);
            output.Write(bytes);
            bytes.Fill(0xff);
        }, cancellationToken);
        afterWrite?.Invoke();
        if (behavior != "missing-complete")
        {
            output.Complete();
        }

        return ValueTask.FromResult(new V1DownserializeResult("d", "evt",
            behavior == "wrong-alias" ? "Another.Event" : "Legacy.Event", 1, "json"));
    }
}
