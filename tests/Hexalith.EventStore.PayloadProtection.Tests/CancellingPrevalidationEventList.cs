using System.Collections;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Cancels during stream shape prevalidation when the first record is indexed.</summary>
/// <param name="source">The cancellation source supplied to the router.</param>
/// <param name="first">The first valid record.</param>
internal sealed class CancellingPrevalidationEventList(
    CancellationTokenSource source,
    CompatibilityEventRecord first) : IReadOnlyList<CompatibilityEventRecord>
{
    /// <inheritdoc/>
    public int Count => 2;

    /// <inheritdoc/>
    public CompatibilityEventRecord this[int index]
    {
        get
        {
            if (index == 0)
            {
                source.Cancel();
                return first;
            }

            if (index == 1)
            {
                return null!;
            }

            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    /// <inheritdoc/>
    public IEnumerator<CompatibilityEventRecord> GetEnumerator()
    {
        yield return this[0];
        yield return this[1];
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
