namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Indexes one immutable actual capture; it never caches validation results across calls.</summary>
internal sealed class HistoricalTransportIndex(IReadOnlyList<QualificationHttpExchange> exchanges)
{
    private readonly Dictionary<string, QualificationHttpExchange[]> _byAddress = exchanges.GroupBy(exchange => exchange.Address, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.OrderBy(exchange => exchange.Started).ToArray(), StringComparer.Ordinal);

    internal IEnumerable<QualificationHttpExchange> Within(string address, DateTimeOffset began, DateTimeOffset ended)
    {
        if (!_byAddress.TryGetValue(address, out var rows)) yield break;
        int low = 0, high = rows.Length;
        while (low < high)
        {
            int middle = low + (high - low) / 2;
            if (rows[middle].Started < began) low = middle + 1; else high = middle;
        }
        for (int index = low; index < rows.Length && rows[index].Started <= ended; index++)
            if (rows[index].Ended <= ended) yield return rows[index];
    }
}
