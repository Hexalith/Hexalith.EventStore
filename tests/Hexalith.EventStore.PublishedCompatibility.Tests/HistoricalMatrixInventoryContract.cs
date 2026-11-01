using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Checks independently grounded append/read/refusal/replay equality relationships for every actual captured actor inventory.</summary>
internal static class HistoricalMatrixInventoryContract
{
    private static readonly IReadOnlyDictionary<(string, string), JsonElement> Expected = Load();

    internal static void Validate(ScenarioObservation observation, IReadOnlyDictionary<string, string> records)
    {
        var actors = new Dictionary<string, int>(StringComparer.Ordinal);
        var values = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var normalized = new List<object>();
        foreach (var pair in records.Where(pair => pair.Key.StartsWith("inventory:", StringComparison.Ordinal)))
        {
            using var document = JsonDocument.Parse(pair.Value); var data = document.RootElement;
            string actor = data.GetProperty("fixture").GetString()!;
            if (!actors.TryGetValue(actor, out int index)) actors.Add(actor, index = actors.Count);
            var hashes = data.GetProperty("hashes");
            int[] sequences = hashes.EnumerateObject().Where(field => field.Name.StartsWith(actor + ":events:", StringComparison.Ordinal))
                .Select(field => int.Parse(field.Name[(actor.Length + ":events:".Length)..], System.Globalization.CultureInfo.InvariantCulture)).Order().ToArray();
            string[] fields = [actor + ":metadata", actor + ":snapshot", .. Enumerable.Range(1, sequences.Length).Select(sequence => actor + ":events:" + sequence)];
            if (!sequences.SequenceEqual(Enumerable.Range(1, sequences.Length)) || !fields.Order(StringComparer.Ordinal)
                .SequenceEqual(hashes.EnumerateObject().Select(field => field.Name).Order(StringComparer.Ordinal)))
                throw new InvalidOperationException("The actual actor inventory lost its exact contiguous observed key closure.");
            var classes = new List<int>();
            foreach (string field in fields)
            {
                var value = hashes.GetProperty(field);
                if (value.ValueKind == JsonValueKind.Null) { classes.Add(-1); continue; }
                string hash = value.GetString()!;
                if (hash.Length != 64 || !hash.All(char.IsAsciiHexDigitLower)) throw new InvalidOperationException("A captured committed value has no actual digest.");
                if (!values.TryGetValue(field, out var observed)) values.Add(field, observed = []);
                int prior = observed.IndexOf(hash); if (prior < 0) { prior = observed.Count; observed.Add(hash); }
                classes.Add(prior);
            }
            normalized.Add(new { Actor = index, Tenant = actor.Split(':')[0], Head = sequences.Length, Values = classes });
        }
        if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(normalized), Expected[(observation.Scenario, observation.Case)]))
            throw new InvalidOperationException("The original append/refusal/replay/restore inventory relationships were erased or changed: " + observation.Scenario + "/" + observation.Case);
    }

    private static IReadOnlyDictionary<(string, string), JsonElement> Load()
    {
        string path = Path.Combine(PublishedScenarioTests.FindRepository(), "tests/Hexalith.EventStore.PublishedCompatibility.Tests/Fixtures/HistoricalMatrixCharacterizationFixture.json");
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        return document.RootElement.GetProperty("rows").EnumerateArray().ToDictionary(row => (row.GetProperty("Scenario").GetString()!, row.GetProperty("Case").GetString()!),
            row => row.GetProperty("InventoryGeometry").Clone());
    }
}
