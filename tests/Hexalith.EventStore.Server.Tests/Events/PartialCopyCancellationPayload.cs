using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Copies a nonempty prefix and then deterministically cancels to test private destination cleanup.</summary>
/// <param name="cancellation">The source used to cancel after partial copying.</param>
internal sealed class PartialCopyCancellationPayload(CancellationTokenSource cancellation) : IReadOnlyPayload
{
    /// <inheritdoc/>
    public int Length => 4;

    /// <summary>Gets how many plaintext bytes were copied before cancellation.</summary>
    internal int CopiedBytes { get; private set; }

    /// <inheritdoc/>
    public void CopyTo(int sourceOffset, Span<byte> destination)
    {
        destination[..2].Fill(7);
        CopiedBytes = 2;
        cancellation.Cancel();
        cancellation.Token.ThrowIfCancellationRequested();
    }
}
