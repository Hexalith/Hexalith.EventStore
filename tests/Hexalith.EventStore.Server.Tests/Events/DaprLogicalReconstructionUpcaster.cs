using System.Globalization;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Transforms V1 units into V2 deltas with a deliberately nontrivial semantic conversion.</summary>
internal sealed class DaprLogicalReconstructionUpcaster(
    Action<IReadOnlyPayload, IBoundedPayloadWriter, IBoundedScratchAllocator, CancellationToken>? hook = null) : IEventUpcaster
{
    /// <inheritdoc/>
    public ValueTask<EventUpcastResult> UpcastAsync(IReadOnlyPayload input, IBoundedPayloadWriter output,
        IBoundedScratchAllocator scratch, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Span<byte> image = stackalloc byte[64];
        input.CopyTo(0, image[..input.Length]);
        var reader = new Utf8JsonReader(image[..input.Length]);
        reader.Read();
        reader.Read();
        reader.Read();
        int units = reader.GetInt32();
        output.Write(Encoding.UTF8.GetBytes("{\"delta\":" + (units * 10 + 3).ToString(CultureInfo.InvariantCulture) + "}"));
        output.Complete();
        hook?.Invoke(input, output, scratch, cancellationToken);
        return ValueTask.FromResult(new EventUpcastResult("d", "evt", 2, "json"));
    }
}
