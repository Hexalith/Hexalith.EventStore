using System.Buffers;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Tracks exact rental ownership and poisons returned arrays to reveal retained pool aliases.</summary>
internal sealed class RecordingExecutableImagePool : ArrayPool<byte>
{
    private readonly HashSet<byte[]> _owned = new(ReferenceEqualityComparer.Instance);
    /// <summary>Gets the rent count.</summary>
    internal int Rents { get; private set; }
    /// <summary>Gets the return count.</summary>
    internal int Returns { get; private set; }
    /// <summary>Gets the largest requested capture allocation.</summary>
    internal int MaximumRequested { get; private set; }
    /// <summary>Gets the outstanding rental count.</summary>
    internal int Outstanding => _owned.Count;
    /// <inheritdoc/>
    public override byte[] Rent(int minimumLength)
    {
        Rents++;
        MaximumRequested = Math.Max(MaximumRequested, minimumLength);
        byte[] bytes = new byte[minimumLength + 17];
        Array.Fill(bytes, (byte)0xa5);
        _owned.Add(bytes);
        return bytes;
    }
    /// <inheritdoc/>
    public override void Return(byte[] array, bool clearArray = false)
    {
        if (!_owned.Remove(array)) throw new InvalidOperationException("An executable buffer was returned twice or by another owner.");
        Returns++;
        Array.Fill(array, (byte)0xcc);
    }
}
