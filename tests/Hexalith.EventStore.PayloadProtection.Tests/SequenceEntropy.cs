namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>
/// Supplies deterministic references and distinct DEKs for collision and concurrency tests.
/// </summary>
internal sealed class SequenceEntropy : IPayloadProtectionEntropy
{
    private readonly Action? _keyReferenceCheckpoint;
    private readonly Exception? _fillException;
    private readonly Queue<string> _references;
    private int _fill;

    /// <summary>Initializes deterministic entropy with an optional post-reference checkpoint.</summary>
    internal SequenceEntropy(
        IEnumerable<string> references,
        Action? keyReferenceCheckpoint = null,
        Exception? fillException = null)
    {
        _references = new Queue<string>(references);
        _keyReferenceCheckpoint = keyReferenceCheckpoint;
        _fillException = fillException;
    }

    /// <summary>Gets how many DEKs were filled.</summary>
    internal int FillCount => _fill;

    /// <inheritdoc/>
    public string CreateKeyReference()
    {
        string result = _references.Dequeue();
        _keyReferenceCheckpoint?.Invoke();
        return result;
    }

    /// <inheritdoc/>
    public void FillDataEncryptionKey(Span<byte> destination)
    {
        destination.Fill(checked((byte)Interlocked.Increment(ref _fill)));
        if (_fillException is not null)
        {
            throw _fillException;
        }
    }
}
