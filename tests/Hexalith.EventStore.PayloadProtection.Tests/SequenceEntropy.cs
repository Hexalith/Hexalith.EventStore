using System.Buffers.Binary;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>
/// Supplies deterministic references and distinct DEKs for collision and concurrency tests.
/// </summary>
internal sealed class SequenceEntropy : IPayloadProtectionEntropy
{
    private readonly Action? _keyReferenceCheckpoint;
    private readonly Exception? _fillException;
    private readonly Queue<string> _references;
    private readonly Lock _sync = new();
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
    internal int FillCount => Volatile.Read(ref _fill);

    /// <inheritdoc/>
    public string CreateKeyReference()
    {
        string result;
        lock (_sync)
        {
            if (!_references.TryDequeue(out result!))
            {
                throw new InvalidOperationException("The deterministic key-reference sequence is exhausted.");
            }
        }

        _keyReferenceCheckpoint?.Invoke();
        return result;
    }

    /// <inheritdoc/>
    public void FillDataEncryptionKey(Span<byte> destination)
    {
        int fill = Interlocked.Increment(ref _fill);
        if (fill <= byte.MaxValue)
        {
            destination.Fill(checked((byte)fill));
        }
        else
        {
            destination.Clear();
            BinaryPrimitives.WriteInt32BigEndian(destination[^sizeof(int)..], fill);
        }

        if (_fillException is not null)
        {
            throw _fillException;
        }
    }
}
