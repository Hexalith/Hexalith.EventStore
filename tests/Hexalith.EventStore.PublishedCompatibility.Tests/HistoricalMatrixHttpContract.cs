using System.Text;

using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Binds actual HTTP bodies/status and finite endpoint geometry to the reviewed consumer scenarios.</summary>
internal static class HistoricalMatrixHttpContract
{
    private static readonly IReadOnlyDictionary<(string, string), Dictionary<string, int>> Geometry = ReadGeometry();

    internal static void Validate(ScenarioObservation observation, IReadOnlyDictionary<string, string> records)
    {
        var gets = records.Where(pair => pair.Key.StartsWith("http-get:", StringComparison.Ordinal)).Select(pair =>
            (Key: pair.Key, Uri: new Uri(pair.Key["http-get:".Length..].Split('#')[0]), Body: JsonSerializer.Deserialize<JsonElement>(pair.Value))).ToArray();
        var geometry = gets.GroupBy(item => Head(item.Uri)).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var expected = Geometry[(observation.Scenario, observation.Case)];
        if (!geometry.Keys.Order(StringComparer.Ordinal).SequenceEqual(expected.Keys.Order(StringComparer.Ordinal))
            || expected.Any(pair => pair.Key == "reminder" ? geometry[pair.Key] < pair.Value : geometry[pair.Key] != pair.Value))
            throw new InvalidOperationException("The measured HTTP endpoint geometry differs from the actual case.");
        var loaded = records.Where(pair => pair.Key.Split('#')[0] is "loaded-Host" or "loaded-Domain")
            .Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value)).ToArray();
        var timings = records.Where(pair => pair.Key.Split('#')[0] == "http-get-observation")
            .Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value)).ToArray();
        if (timings.Length != gets.Length) throw new InvalidOperationException("Every actual GET requires its independent bounded transport observation.");
        var matched = new HashSet<int>();
        DateTimeOffset? firstReminderRead = null, lastReminderRead = null;
        foreach (var get in gets)
        {
            var data = get.Body;
            int ordinal = HistoricalMatrixScenarioContract.Ordinal(get.Key);
            var candidates = timings.Select((value, index) => (Value: value, Index: index))
                .Where(item => item.Value.GetProperty("address").GetString() == get.Uri.OriginalString).OrderBy(item => item.Value.GetProperty("began").GetDateTimeOffset()).ToArray();
            if (ordinal >= candidates.Length) throw new InvalidOperationException("The GET body has no independently captured request.");
            var timing = candidates[ordinal];
            if (!matched.Add(timing.Index) || timing.Value.GetProperty("status").GetInt32() != 200
                || timing.Value.GetProperty("bodyHash").GetString() != QualificationRuntime.Hash(Encoding.UTF8.GetBytes(data.GetRawText()))
                || timing.Value.GetProperty("began").GetDateTimeOffset() < observation.Started
                || timing.Value.GetProperty("ended").GetDateTimeOffset() > observation.Ended
                || timing.Value.GetProperty("ended").GetDateTimeOffset() < timing.Value.GetProperty("began").GetDateTimeOffset())
                throw new InvalidOperationException("The GET body lost its unique successful case-contained transport observation.");
            if (Head(get.Uri) == "reminder")
            {
                firstReminderRead ??= timing.Value.GetProperty("ended").GetDateTimeOffset();
                lastReminderRead = timing.Value.GetProperty("ended").GetDateTimeOffset();
            }
            switch (Head(get.Uri))
            {
                case "identity":
                    if (!loaded.Any(identity => JsonElement.DeepEquals(identity, data)))
                        throw new InvalidOperationException("The identity HTTP body differs from the independently loaded consumer.");
                    break;
                case "reader":
                    string actor = Uri.UnescapeDataString(get.Uri.AbsolutePath["/reader/".Length..]);
                    var profiles = records.Where(pair => pair.Key.Split('#')[0] == "runtime-profile")
                        .Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value)).Where(profile => profile.GetProperty("hostAddress").GetString() == get.Uri.GetLeftPart(UriPartial.Authority)).ToArray();
                    var readerTarget = JsonSerializer.Deserialize<JsonElement>(records["actual-reader-target"]);
                    string firstActor = records.Where(pair => pair.Key.StartsWith("inventory:", StringComparison.Ordinal)).Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value).GetProperty("fixture").GetString()!).First();
                    if (readerTarget.GetProperty("actor").GetString() != actor || actor != firstActor || readerTarget.GetProperty("address").GetString() != get.Uri.AbsoluteUri
                        || get.Uri.Query.Length != 0 || get.Uri.Fragment.Length != 0 || profiles.Length != 1 || profiles[0].GetProperty("hostProcessId").GetInt32() <= 0
                        || profiles[0].GetProperty("hostProcessId").GetInt32() != readerTarget.GetProperty("hostProcessId").GetInt32()
                        || !records.Keys.Any(key => key.Split('#')[0] == "inventory:" + actor)
                        || !gets.Any(item => item.Uri.AbsolutePath == "/identity" && item.Uri.GetLeftPart(UriPartial.Authority) == get.Uri.GetLeftPart(UriPartial.Authority)))
                        throw new InvalidOperationException("The reader left its actual independently loaded host or exact persisted actor.");
                    if (!records.Where(pair => pair.Key.Split('#')[0] == "actual-versioned-reader-result")
                        .Any(pair => JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(pair.Value), data)))
                        throw new InvalidOperationException("The actual reader HTTP body differs from its measured logical operation.");
                    break;
                case "status-fixture":
                    var name = get.Uri.AbsolutePath.Split('/').Last();
                    bool old = observation.Case.StartsWith("3.70.1-", StringComparison.Ordinal);
                    bool present = data.TryGetProperty("retryable", out var retryable);
                    bool invalidRetryable = old ? present : !present || (name.EndsWith("-legacy", StringComparison.Ordinal)
                        ? retryable.ValueKind != JsonValueKind.Null
                        : retryable.ValueKind is not (JsonValueKind.True or JsonValueKind.False) || retryable.GetBoolean() != name.EndsWith("-automatic", StringComparison.Ordinal));
                    if (data.GetProperty("status").GetInt32() != 4 || data.GetProperty("aggregateId").GetString() != "fixture-counter"
                        || data.GetProperty("eventCount").GetInt32() != 1 || data.GetProperty("messageId").GetString() != name || invalidRetryable)
                        throw new InvalidOperationException("The persisted status HTTP body lost its exact tri-state or characterized legacy absence.");
                    break;
                case "stats":
                    if (!records.Where(pair => pair.Key.Split('#')[0] == "runtime-profile").Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value))
                        .Any(profile => get.Uri.AbsoluteUri == profile.GetProperty("domainAddress").GetString() + "/stats"))
                        throw new InvalidOperationException("The handler statistics left the actual domainAddress /stats endpoint.");
                    foreach (var key in new[] { "applyCalls", "handleCalls", "queryReads", "queryRequests" })
                        if (data.GetProperty(key).GetInt32() < 0) throw new InvalidOperationException("The actual handler statistics were erased.");
                    if (observation.Case.EndsWith("-registered-manifest-production-hop", StringComparison.Ordinal)
                        && (data.GetProperty("applyCalls").GetInt32() != 0 || data.GetProperty("handleCalls").GetInt32() != 0))
                        throw new InvalidOperationException("The missing-callable refusal must precede every domain callback.");
                    break;
                case "manifest":
                    if (!data.GetProperty("registered").GetBoolean() || data.GetProperty("fingerprint").GetString() is not { Length: 64 } digest
                        || !digest.All(char.IsAsciiHexDigitLower)) throw new InvalidOperationException("The manifest HTTP body lost its actual registration.");
                    break;
                case "manifest-refusal":
                    if (!records.Where(pair => pair.Key.Split('#')[0] == "production-manifest-refusal")
                        .Any(pair => JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(pair.Value), data)))
                        throw new InvalidOperationException("The manifest HTTP body lost its actual refusal cause.");
                    break;
                case "reminder":
                    if (data.GetProperty("submitted").GetInt32() is < 0 or > 1) throw new InvalidOperationException("The reminder HTTP counter is not the actual one-effect lifecycle.");
                    break;
                default: throw new InvalidOperationException("The historical case has no such measured HTTP endpoint.");
            }
        }
        var statuses = gets.Where(item => Head(item.Uri) == "status-fixture").Select(item => item.Uri.AbsolutePath.Split('-').Last()).ToArray();
        if (statuses.Length > 0 && !statuses.SequenceEqual(new[] { "legacy", "terminal", "automatic" }))
            throw new InvalidOperationException("The HTTP case must independently round-trip every persisted status variant.");
        var reminder = gets.Where(item => Head(item.Uri) == "reminder").Select(item => item.Body.GetProperty("submitted").GetInt32()).ToArray();
        if (reminder.Length > 0 && (reminder[0] != 0 || reminder[^1] != 1 || !reminder.SequenceEqual(reminder.Order())))
            throw new InvalidOperationException("The natural reminder lost pre-due zero and exactly one persisted submission.");
        if (reminder.Length > 0)
        {
            var window = JsonSerializer.Deserialize<JsonElement>(records["bounded-reminder-registration-restart-window"]);
            DateTimeOffset due = window.GetProperty("due").GetDateTimeOffset();
            if (firstReminderRead >= due || lastReminderRead < due)
                throw new InvalidOperationException("The actual reminder HTTP reads must straddle natural delivery.");
        }
        foreach (var post in records.Where(pair => pair.Key.Split('#')[0] == "http-post").Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value)))
            if (post.GetProperty("status").GetInt32() != 200 || post.GetProperty("began").GetDateTimeOffset() < observation.Started
                || post.GetProperty("ended").GetDateTimeOffset() > observation.Ended)
                throw new InvalidOperationException("The actual successful HTTP mutation lost its successful status or case-contained interval.");
        HistoricalMatrixHttpSequenceContract.Validate(observation, records);
    }

    private static string Head(Uri uri) => uri.AbsolutePath.Trim('/').Split('/')[0];
    private static IReadOnlyDictionary<(string, string), Dictionary<string, int>> ReadGeometry()
    {
        string fixture = Path.Combine(PublishedScenarioTests.FindRepository(), "tests/Hexalith.EventStore.PublishedCompatibility.Tests/Fixtures/HistoricalMatrixCharacterizationFixture.json");
        using var source = JsonDocument.Parse(File.ReadAllBytes(fixture));
        return source.RootElement.GetProperty("rows").EnumerateArray().ToDictionary(
            row => (row.GetProperty("Scenario").GetString()!, row.GetProperty("Case").GetString()!),
            row => row.GetProperty("HttpGetPaths").EnumerateObject().ToDictionary(pair => pair.Name, pair => pair.Value.GetInt32(), StringComparer.Ordinal));
    }
}
