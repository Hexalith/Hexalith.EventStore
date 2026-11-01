using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Retains each independently measured field/default relationship and actual three-hop input/output commitments.</summary>
internal static class HistoricalMatrixWireContract
{
    private static readonly JsonElement Fixture = LoadFixture();
    internal static JsonElement ReadFixture() => Fixture;
    private static JsonElement LoadFixture()
    {
        using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(PublishedScenarioTests.FindRepository(),
            "tests/Hexalith.EventStore.PublishedCompatibility.Tests/Fixtures/HistoricalWireGeometry.json")));
        return json.RootElement.Clone();
    }

    internal static void Validate(ScenarioObservation observation, IReadOnlyList<HistoricalExecutionCommand> commands)
    {
        if (observation.Scenario is not ("query-wire" or "projection-wire") || !observation.Case.Contains("-to-", StringComparison.Ordinal)) return;
        var geometry = ReadFixture().GetProperty("rows").EnumerateArray().Single(row => row.GetProperty("Scenario").GetString() == observation.Scenario
            && row.GetProperty("Case").GetString() == observation.Case).GetProperty("Hops");
        var actual = (observation.Measurements ?? new Dictionary<string, string>()).Where(pair => pair.Key.Split('#')[0] == "probe-command")
            .Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value)).Select(record => commands.Single(command =>
                command.Record.GetProperty("Started").GetDateTimeOffset() == record.GetProperty("Started").GetDateTimeOffset()
                && command.Record.GetProperty("Ended").GetDateTimeOffset() == record.GetProperty("Ended").GetDateTimeOffset()
                && command.Record.GetProperty("OutputHash").GetString() == record.GetProperty("OutputHash").GetString())).ToArray();
        if (actual.Length != geometry.GetArrayLength()) throw new InvalidOperationException("The wire case lost an original measured hop.");
        JsonElement source = observation.Scenario == "query-wire"
            ? PublishedScenarioTests.ParseProbeOutput(commands.Single(command => command.Operation == "signed-query").Record.GetProperty("StandardOutput").GetString()!).GetProperty("value")
            : ReadFixture().GetProperty("projectionSource");
        var sourceFields = HistoricalWireInput.HashFields(source);
        string? previousWire = null;
        for (int i = 0; i < actual.Length; i++)
        {
            RequireInput(actual[i], sourceFields, previousWire);
            var result = PublishedScenarioTests.ParseProbeOutput(actual[i].Record.GetProperty("StandardOutput").GetString()!);
            var value = result.GetProperty("value");
            var expected = geometry[i];
            if (!value.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
                .SequenceEqual(expected.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)))
                throw new InvalidOperationException("The wire result erased or added a characterized field.");
            foreach (var field in expected.EnumerateObject())
            {
                var observed = value.GetProperty(field.Name);
                bool matches = field.Value.GetString() switch
                {
                    "source" => HistoricalWireInput.HashValue(observed) == sourceFields[field.Name],
                    "null" => observed.ValueKind == JsonValueKind.Null,
                    "false" => observed.ValueKind == JsonValueKind.False,
                    "true" => observed.ValueKind == JsonValueKind.True,
                    "zero" => observed.ValueKind == JsonValueKind.Number && observed.GetDecimal() == 0,
                    "empty" => observed.ValueKind == JsonValueKind.String && observed.GetString() == "",
                    "empty-array" => observed.ValueKind == JsonValueKind.Array && observed.GetArrayLength() == 0,
                    _ => false,
                };
                if (!matches) throw new InvalidOperationException("The wire result changed its actual preserved field or characterized historical deficit.");
            }
            previousWire = result.GetProperty("wire").GetString();
        }
        if (observation.Error is not null)
        {
            var failed = commands.Where(command => command.Record.GetProperty("Started").GetDateTimeOffset() >= observation.Started
                && command.Record.GetProperty("Ended").GetDateTimeOffset() <= observation.Ended && command.Record.GetProperty("ExitCode").GetInt32() == 134).ToArray();
            if (failed.Length != 1) throw new InvalidOperationException("The characterized XML refusal lost its actual next input.");
            RequireInput(failed[0], sourceFields, previousWire);
        }
    }

    private static void RequireInput(HistoricalExecutionCommand command, IReadOnlyDictionary<string, string> fields, string? previous)
    {
        if (command.Wire is null || (previous is null
            ? command.Wire.Fields is null || !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(command.Wire.Fields), JsonSerializer.SerializeToElement(fields))
            : command.Wire.ValueSha256 != QualificationRuntime.Hash(Encoding.UTF8.GetBytes(previous))))
            throw new InvalidOperationException("A wire hop did not consume its real signed-query/projection source or previous actual output.");
    }
}
