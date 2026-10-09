using System.Collections;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Observes the actual caller-supplied result count and indexer callbacks.</summary>
/// <param name="item">The observed serialized event input.</param>
internal sealed class DaprLogicalCommandStateEventList(IEventPayload item) : IReadOnlyList<IEventPayload>
{
    /// <summary>Gets or sets a callback after list construction.</summary>
    internal Action<string>? Hook
    {
        get; set;
    }
    /// <inheritdoc/>
    public int Count
    {
        get
        {
            Hook?.Invoke("count");
            return 1;
        }
    }
    /// <inheritdoc/>
    public IEventPayload this[int index] { get { Hook?.Invoke("indexer"); return index == 0 ? item : throw new IndexOutOfRangeException(); } }
    /// <inheritdoc/>
    public IEnumerator<IEventPayload> GetEnumerator() => new[] { item }.AsEnumerable().GetEnumerator();
    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
