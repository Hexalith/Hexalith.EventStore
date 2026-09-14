namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>
/// Supplies deterministic references and distinct DEKs for collision and concurrency tests.
/// </summary>
internal sealed class SequenceEntropy(IEnumerable<string> references) : IPayloadProtectionEntropy {
    private readonly Queue<string> _references = new(references);
    private int _fill;

    /// <summary>Gets how many DEKs were filled.</summary>
    internal int FillCount => _fill;

    /// <inheritdoc/>
    public string CreateKeyReference() => _references.Dequeue();

    /// <inheritdoc/>
    public void FillDataEncryptionKey(Span<byte> destination) {
        destination.Fill(checked((byte)Interlocked.Increment(ref _fill)));
    }
}
