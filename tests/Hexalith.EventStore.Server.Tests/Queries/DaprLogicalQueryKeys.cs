using System.Collections;

namespace Hexalith.EventStore.Server.Tests.Queries;
/// <summary>Exposes application Count/indexer callbacks at the actual bulk store boundary.</summary>
/// <param name = "fixture">The current isolated callback context.</param>
public sealed class DaprLogicalQueryKeys(DaprLogicalQueryFixture fixture) : IReadOnlyList<string>
{
    /// <inheritdoc/>
    public int Count
    {
        get
        {
            fixture.Callback("bulk-count");
            return 2;
        }
    }

    /// <inheritdoc/>
    public string this[int index]
    {
        get
        {
            fixture.Callback("bulk-index");
            return index == 0 ? "item:a" : "item:b";
        }
    }

    /// <inheritdoc/>
    public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)["item:a", "item:b"]).GetEnumerator();
    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
