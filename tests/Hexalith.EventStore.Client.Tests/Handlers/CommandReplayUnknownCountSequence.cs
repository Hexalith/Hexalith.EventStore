using System.Collections;

namespace Hexalith.EventStore.Client.Tests.Handlers;

/// <summary>Supplies command-state input without a count so incremental admission must stop it.</summary>
internal sealed class CommandReplayUnknownCountSequence(int count, object? item = null) : IEnumerable
{
    /// <summary>Gets the number of entries requested before admission stopped enumeration.</summary>
    internal int EntriesRead { get; private set; }

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
