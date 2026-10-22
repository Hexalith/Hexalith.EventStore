using System.Collections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Provides one unambiguous typed immutable view over shared retained digest bytes.</summary>
internal sealed class ProjectionSourceAdmittedDigestList<T>(ProjectionSourceAdmittedDigests table,
    Func<ProjectionSourceAdmittedDigests, int, T> read,
    Func<ProjectionSourceAdmittedDigests, IEnumerable<T>>? enumerate = null) : IReadOnlyList<T>
{
    /// <summary>Gets the shared immutable digest authority without copying its data.</summary>
    internal ProjectionSourceAdmittedDigests Table => table;

    /// <inheritdoc/>
    public int Count => table.Count;

    /// <inheritdoc/>
    public T this[int index] => read(table, index);

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => (enumerate?.Invoke(table) ?? Read()).GetEnumerator();

    private IEnumerable<T> Read()
    {
        for (int index = 0; index < Count; index++) yield return this[index];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
