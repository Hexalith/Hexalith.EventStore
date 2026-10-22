using System.Collections;

using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Changes its advertised count to catch admission that rereads a caller-owned collection.</summary>
internal sealed class ChangingCountWireEvents(DomainServiceWireEvent item) : IReadOnlyList<DomainServiceWireEvent>
{
    /// <summary>Gets how often intake observed the caller-owned count.</summary>
    internal int CountReads { get; private set; }

    /// <inheritdoc/>
    public int Count => ++CountReads == 1 ? 1 : 1001;

    /// <inheritdoc/>
    public DomainServiceWireEvent this[int index] => index == 0 ? item : throw new ArgumentOutOfRangeException(nameof(index));

    /// <inheritdoc/>
    public IEnumerator<DomainServiceWireEvent> GetEnumerator() => ((IEnumerable<DomainServiceWireEvent>)new[] { item }).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
