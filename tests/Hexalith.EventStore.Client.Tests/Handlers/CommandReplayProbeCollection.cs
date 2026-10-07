using System.Collections;

namespace Hexalith.EventStore.Client.Tests.Handlers;

/// <summary>Observes whether count refusal precedes enumeration of command-state input.</summary>
internal sealed class CommandReplayProbeCollection(int count, object? item = null) : ICollection
{
    /// <summary>Gets how many times admission reads the declared source count.</summary>
    internal int CountReads { get; private set; }

    /// <summary>Gets how many source entries admission asks the enumerator to yield.</summary>
    internal int EntriesRead { get; private set; }

    /// <inheritdoc/>
    public int Count
    {
        get { CountReads++; return count; }
    }

    /// <inheritdoc/>
    public bool IsSynchronized => false;

    /// <inheritdoc/>
    public object SyncRoot => this;

    /// <inheritdoc/>
    public void CopyTo(Array array, int index) => throw new NotSupportedException();

    /// <inheritdoc/>
    public IEnumerator GetEnumerator()
    {
        for (int index = 0; index < count; index++)
        {
            EntriesRead++;
            yield return item;
        }
    }
}
