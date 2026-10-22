using System.Collections;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Tests.Handlers;

public sealed class DetachedSnapshotTail(EventEnvelope envelope, Action onCount, Action onEnumeration) : IReadOnlyList<EventEnvelope>, ICollection
{
    public int Count { get { onCount(); return 1; } }
    public EventEnvelope this[int index] => index == 0 ? envelope : throw new IndexOutOfRangeException();
    public bool IsSynchronized => false;
    public object SyncRoot => this;
    public void CopyTo(Array array, int index) => throw new InvalidOperationException("Forbidden CopyTo.");
    public IEnumerator<EventEnvelope> GetEnumerator() { onEnumeration(); yield return envelope; }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
