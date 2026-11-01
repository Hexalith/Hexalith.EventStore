using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Binds ordered case geometry and real probe operations to the reviewed producer's fixed scenarios.</summary>
internal static class HistoricalMatrixScenarioContract
{
    private static readonly IReadOnlyDictionary<(string Scenario, string Case), (string Host, string Domain)[]> Profiles = ReadProfiles();

    internal static void ValidateProfiles(ScenarioObservation observation, IReadOnlyDictionary<string, string> records)
    {
        var actual = records.Where(pair => pair.Key.Split('#')[0] == "runtime-profile").Select(pair =>
        {
            using var document = JsonDocument.Parse(pair.Value);
            return (Key(document.RootElement.GetProperty("host")), Key(document.RootElement.GetProperty("domain")));
        }).ToArray();
        if (!Profiles.TryGetValue((observation.Scenario, observation.Case), out var expected) || !actual.SequenceEqual(expected))
            throw new InvalidOperationException("The ordered source/destination consumer profiles differ from this historical case.");
    }

    internal static string Key(ConsumerBuild build)
        => build.Version + (build.Source is null ? "-package-" : "-source-") + build.Role;

    internal static (string Build, string Operation, string? Kind, string? Serializer) Probe(ScenarioObservation observation, int index)
    {
        string name = observation.Case;
        if (observation.Scenario == "provenance") return (name, "identity", null, null);
        if (observation.Scenario == "legacy-metadata" && name.EndsWith("-three-argument-constructor", StringComparison.Ordinal))
            return (name[..^"-three-argument-constructor".Length], "legacy-constructor", null, null);
        if (observation.Scenario is "legacy-metadata" or "metadata-read")
            return (name.Split('-')[0] + "-package-Probe", "metadata", null, null);
        if (observation.Scenario == "mixed-api" && name.EndsWith("-cursor", StringComparison.Ordinal))
            return (name[..^"-cursor".Length], "cursor", null, null);
        if (observation.Scenario == "mixed-api" && name.StartsWith("client-", StringComparison.Ordinal))
        {
            string[] fields = name.Split('-');
            return (fields[1] + (fields[2] == "Debug" ? "-source-Probe" : "-package-Probe"), "cross-client", null, null);
        }
        if (observation.Scenario == "query-wire" && name == "real-signed-admission-and-refusals")
            return ("3.128.2-package-Probe", "signed-query", null, null);
        if (observation.Scenario is "query-wire" or "projection-wire")
        {
            string serializer = name.EndsWith("-xml", StringComparison.Ordinal) ? "xml" : "json";
            string[] direction = name[..^(serializer.Length + 1)].Split("-to-", StringSplitOptions.None);
            if (direction.Length != 2 || index is < 0 or > 2) throw new InvalidOperationException("Unknown historical wire execution.");
            string operation = serializer == "xml" && index == 0 ? "make-wire" : "wire";
            return (direction[index == 1 ? 1 : 0], operation, observation.Scenario == "query-wire" ? "query" : "projection",
                operation == "make-wire" ? null : serializer);
        }
        throw new InvalidOperationException("The case has no declared historical probe operation.");
    }

    internal static int Ordinal(string key) => key.Contains('#', StringComparison.Ordinal)
        ? int.Parse(key.Split('#')[1], System.Globalization.CultureInfo.InvariantCulture) - 1 : 0;

    private static string Key(JsonElement build) => build.GetProperty("Version").GetString()
        + (build.GetProperty("Source").ValueKind == JsonValueKind.Null ? "-package-" : "-source-") + build.GetProperty("Role").GetString();

    private static IReadOnlyDictionary<(string, string), (string, string)[]> ReadProfiles()
    {
        string fixture = Path.Combine(PublishedScenarioTests.FindRepository(), "tests/Hexalith.EventStore.PublishedCompatibility.Tests/Fixtures/HistoricalMatrixCharacterizationFixture.json");
        using var document = JsonDocument.Parse(File.ReadAllBytes(fixture));
        return document.RootElement.GetProperty("rows").EnumerateArray().ToDictionary(
            row => (row.GetProperty("Scenario").GetString()!, row.GetProperty("Case").GetString()!),
            row => row.GetProperty("Profiles").EnumerateArray().Select(profile =>
                (profile.GetProperty("host").GetString()!, profile.GetProperty("domain").GetString()!)).ToArray());
    }
}
