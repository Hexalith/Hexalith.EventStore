namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Counts only executed checks, preserving every failed assertion within a case.</summary>
internal sealed class MeasuredAssertions
{
    /// <summary>Gets the number of actual evaluated assertions.</summary>
    internal int Count { get; private set; }

    /// <summary>Gets failed assertion identifiers.</summary>
    internal List<string> Failures { get; } = [];

    /// <summary>Gets concrete fixture, command and inventory observations without inflating assertion counts.</summary>
    internal Dictionary<string, string> Measurements { get; } = new(StringComparer.Ordinal);

    /// <summary>Retains each observation under a unique key, including repeated before/after inventories.</summary>
    internal void Record(string key, string value)
    {
        string candidate = key;
        int suffix = 1;
        while (Measurements.ContainsKey(candidate)) candidate = key + "#" + ++suffix;
        string safe = HistoricalSafeJson.Text(value);
        Measurements.Add(candidate, safe);
        if (safe != value) Measurements.Add("record-byte-commitment:" + candidate, System.Text.Json.JsonSerializer.Serialize(new
            { sha256 = QualificationRuntime.Hash(System.Text.Encoding.UTF8.GetBytes(value)) }));
    }

    /// <summary>Evaluates one named invariant.</summary>
    internal void Check(bool condition, string assertion)
    {
        Count++;
        if (!condition) Failures.Add(assertion);
    }
}
