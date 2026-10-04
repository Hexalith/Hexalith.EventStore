using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Contracts.Tests.Events;

/// <summary>Mutates caller-owned page input at the payload copying boundary.</summary>
internal sealed class CallbackRawPageTestPayload(Action callback) : IReadOnlyPayload
{
    /// <inheritdoc/>
    public int Length => 1;

    /// <inheritdoc/>
    public void CopyTo(int sourceOffset, Span<byte> destination)
    {
        callback();
        destination.Fill(1);
    }
}
