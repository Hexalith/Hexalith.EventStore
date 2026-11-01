using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Binds retained actual fixture mutations to the required corruption and its owning HTTP request.</summary>
internal static class HistoricalMatrixFixtureInputContract
{
    internal static void Validate(ScenarioObservation observation, IReadOnlyDictionary<string, string> records)
    {
        if (observation.Scenario != "invalid-evidence") return;
        string? corruption = new[] { "floor-0", "floor-14", "unreadable", "protected-mismatch", "unknown-type", "version-987" }
            .SingleOrDefault(kind => observation.Case.Contains("-" + kind + "-", StringComparison.Ordinal));
        if (observation.Case.EndsWith("-head12-floor0", StringComparison.Ordinal)) corruption = "floor-0";
        if (observation.Case.EndsWith("-head12-floor14", StringComparison.Ordinal)) corruption = "floor-14";
        if (corruption is null) throw new InvalidOperationException("The invalid-evidence case has no reviewed fixture corruption.");
        var posts = records.Where(record => record.Key.Split('#')[0] == "fixture-http-post")
            .Select(record => JsonSerializer.Deserialize<JsonElement>(record.Value)).ToArray();
        if (posts.Length != 1 || !posts[0].GetProperty("request").TryGetProperty("fixture", out var fixture)
            || fixture.ValueKind != JsonValueKind.Array || fixture.GetArrayLength() != 1)
            throw new InvalidOperationException("The actual fixture corruption input was not retained.");
        var operation = fixture[0].Deserialize<HistoricalFixtureOperation>()
            ?? throw new InvalidOperationException("The actual fixture mutation is unknown.");
        string actor = records.Where(record => record.Key.StartsWith("inventory:", StringComparison.Ordinal))
            .Select(record => JsonSerializer.Deserialize<JsonElement>(record.Value).GetProperty("fixture").GetString()!).First();
        string suffix = corruption.StartsWith("floor-", StringComparison.Ordinal) ? ":metadata" : ":events:2";
        string address = posts[0].GetProperty("address").GetString()!;
        if (operation.Operation != "upsert" || operation.Key != actor + suffix
            || operation.ValueSha256 is not { Length: 64 } digest || !digest.All(char.IsAsciiHexDigitLower)
            || posts[0].GetProperty("status").GetInt32() != 200
            || posts[0].GetProperty("began").GetDateTimeOffset() < observation.Started
            || posts[0].GetProperty("ended").GetDateTimeOffset() > observation.Ended
            || !records.Where(record => record.Key.Split('#')[0] == "runtime-profile")
                .Select(record => JsonSerializer.Deserialize<JsonElement>(record.Value))
                .Any(profile => address == profile.GetProperty("hostAddress").GetString() + "/fixture/" + Uri.EscapeDataString(actor)))
            throw new InvalidOperationException("The actual fixture mutation lost its actor, key or owning request.");
        bool matches = corruption switch
        {
            "floor-0" => operation.RetainedFloor == 0,
            "floor-14" => operation.RetainedFloor == 14,
            "version-987" => operation.MetadataVersion == 987,
            "protected-mismatch" => operation.SerializationFormat == "protected+json",
            "unknown-type" => operation.EventTypeNameSha256 == Hash("Unknown.Event"),
            "unreadable" => operation.PayloadSha256 == Hash(Convert.ToBase64String("invalid-json"u8.ToArray())),
            _ => false,
        };
        if (!matches) throw new InvalidOperationException("The actual fixture mutation differs from its required corruption case.");
    }

    private static string Hash(string value) => QualificationRuntime.Hash(Encoding.UTF8.GetBytes(value));
}
