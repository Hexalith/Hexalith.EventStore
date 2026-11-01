using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Binds real ordered durable-cursor and declared-handler HTTP operations, including safe input/output coordinates.</summary>
internal static class HistoricalMatrixHttpSequenceContract
{
    internal static void Validate(ScenarioObservation observation, IReadOnlyDictionary<string, string> records)
    {
        bool cursor = observation.Case.EndsWith("-durably-applied-watermark-cursor", StringComparison.Ordinal);
        bool handler = observation.Case.EndsWith("-production-declared-handler-discovery-transport", StringComparison.Ordinal);
        if (!cursor && !handler) return;
        var posts = records.Where(pair => pair.Key.Split('#')[0] == "http-post").Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value)).ToArray();
        if (posts.Length != (cursor ? 11 : 7)) throw new InvalidOperationException("The actual ordered HTTP operation sequence was erased.");
        var hosts = records.Where(pair => pair.Key.Split('#')[0] == "loaded-Host").Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value)).ToArray();
        var identities = records.Where(pair => pair.Key.StartsWith("http-get:", StringComparison.Ordinal) && new Uri(pair.Key["http-get:".Length..].Split('#')[0]).AbsolutePath == "/identity")
            .Select(pair => (Uri: new Uri(pair.Key["http-get:".Length..].Split('#')[0]), Body: JsonSerializer.Deserialize<JsonElement>(pair.Value)))
            .Where(item => hosts.Any(host => JsonElement.DeepEquals(host, item.Body))).ToArray();
        if (identities.Length != (cursor ? 2 : 1)) throw new InvalidOperationException("The mutation sequence lost its actual loaded Host transport coordinates.");
        var timings = records.Where(pair => pair.Key.Split('#')[0] == "http-get-observation").Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value)).ToArray();
        var firstHost = timings.Single(timing => timing.GetProperty("address").GetString() == identities[0].Uri.OriginalString);
        if (firstHost.GetProperty("ended").GetDateTimeOffset() > posts[0].GetProperty("began").GetDateTimeOffset())
            throw new InvalidOperationException("The mutation precedes its actual loaded Host identity.");
        if (cursor)
        {
            var restarted = timings.Single(timing => timing.GetProperty("address").GetString() == identities[1].Uri.OriginalString);
            if (identities[0].Uri.Authority == identities[1].Uri.Authority
                || restarted.GetProperty("began").GetDateTimeOffset() < posts[1].GetProperty("ended").GetDateTimeOffset()
                || restarted.GetProperty("ended").GetDateTimeOffset() > posts[2].GetProperty("began").GetDateTimeOffset())
                throw new InvalidOperationException("The actual cursor replay lost its independently reloaded Host after issuance.");
        }
        string[] paths = cursor ? ["project-model", "cursor-model", "cursor-model", "cursor-model", "cursor-model", "cursor-model", "cursor-model", "cursor-model", "cursor-model", "project-model", "cursor-model"]
            : ["declare-query-handlers", "project-model", "query-route", "query-route", "query-route", "query-route", "query-route"];
        string? aggregate = null;
        for (int i = 0; i < posts.Length; i++)
        {
            var uri = new Uri(posts[i].GetProperty("address").GetString()!); string[] parts = uri.AbsolutePath.Trim('/').Split('/');
            string authority = identities[cursor && i >= 2 ? 1 : 0].Uri.Authority;
            if (uri.Authority != authority || uri.Query.Length != 0 || uri.Fragment.Length != 0 || parts[0] != paths[i]
                || parts.Length != (paths[i] == "declare-query-handlers" ? 1 : 2)
                || i > 0 && posts[i].GetProperty("began").GetDateTimeOffset() < posts[i - 1].GetProperty("ended").GetDateTimeOffset())
                throw new InvalidOperationException("The actual ordered POST path, host or sequential interval differs from its case.");
            if (parts.Length == 2)
            {
                aggregate ??= parts[1];
                if (parts[1] != aggregate || aggregate.Length == 0 || !aggregate.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
                    throw new InvalidOperationException("The actual HTTP mutations no longer address one exact fixture aggregate.");
            }
        }
        if (cursor) ValidateCursor(posts);
        else ValidateHandler(posts, records);
    }

    private static void ValidateCursor(JsonElement[] posts)
    {
        string? cursor = posts[1].GetProperty("response").GetProperty("cursorHash").GetString();
        if (cursor is not { Length: 64 } || !cursor.All(char.IsAsciiHexDigitLower)) throw new InvalidOperationException("The actual issued cursor commitment was erased.");
        string[] modes = ["valid", "valid", "foreign-actor", "foreign-tenant", "foreign-domain", "changed-filter", "changed-query", "tampered", "valid"];
        int ordinal = 0;
        for (int i = 0; i < posts.Length; i++)
        {
            var input = posts[i].GetProperty("request"); var output = posts[i].GetProperty("response");
            if (i is 0 or 9)
            {
                long position = i == 0 ? 987 : 988, sequence = i == 0 ? 12 : 13;
                if (input.GetProperty("position").GetInt64() != position || input.GetProperty("sequence").GetInt64() != sequence
                    || output.GetProperty("watermark").GetInt64() != position || output.GetProperty("sequence").GetInt64() != sequence)
                    throw new InvalidOperationException("The actual durable model mutation lost its exact watermark and sequence.");
            }
            else
            {
                bool decoded = ordinal < 2;
                if (input.GetProperty("mode").GetString() != modes[ordinal]
                    || input.GetProperty("cursorHash").GetString() != (ordinal == 0 ? null : cursor)
                    || output.GetProperty("decoded").GetBoolean() != decoded
                    || decoded && output.GetProperty("position").GetString() != "987"
                    || !decoded && output.GetProperty("position").ValueKind != JsonValueKind.Null
                    || output.GetProperty("modelWatermark").GetInt64() != (ordinal == 8 ? 988 : 987)
                    || output.GetProperty("cursorHash").GetString() is not { Length: 64 } returnedCursor
                    || !returnedCursor.All(char.IsAsciiHexDigitLower)
                    || (returnedCursor == cursor) != (modes[ordinal] != "tampered"))
                    throw new InvalidOperationException("The actual cursor replay, refusal or watermark advancement lost its exact binding.");
                ordinal++;
            }
        }
    }

    private static void ValidateHandler(JsonElement[] posts, IReadOnlyDictionary<string, string> records)
    {
        var discovery = JsonSerializer.Deserialize<JsonElement>(records["production-handler-discovery"]);
        if (!JsonElement.DeepEquals(posts[0].GetProperty("response").GetProperty("declaration"), discovery)
            || posts[0].GetProperty("responseHash").GetString() != QualificationRuntime.Hash(Encoding.UTF8.GetBytes(records["production-handler-discovery"])))
            throw new InvalidOperationException("The declaration POST differs from actual production handler discovery.");
        string[] modes = ["valid", "missing", "forged", "foreign-tenant", "foreign-actor"];
        if (posts[1].GetProperty("request").GetProperty("position").GetInt64() != 987 || posts[1].GetProperty("request").GetProperty("sequence").GetInt64() != 12)
            throw new InvalidOperationException("The actual production handler fixture lost its durable model input.");
        for (int i = 0; i < modes.Length; i++)
        {
            var input = posts[i + 2].GetProperty("request"); var response = posts[i + 2].GetProperty("response");
            string raw = records["production-handler-" + modes[i]]; var body = JsonSerializer.Deserialize<JsonElement>(raw);
            if (input.GetProperty("mode").GetString() != modes[i] || input.GetProperty("serializer").GetString() != "json"
                || input.GetProperty("route").GetString() != "declared-handler"
                || response.GetProperty("success").GetBoolean() != (i == 0)
                || posts[i + 2].GetProperty("responseHash").GetString() != HistoricalRecordCommitment.Hash(records, "production-handler-" + modes[i])
                || !JsonElement.DeepEquals(response.GetProperty("result"), body))
                throw new InvalidOperationException("The production handler result is not bound to its actual successful HTTP operation.");
        }
        var stats = records.Where(pair => pair.Key.StartsWith("http-get:", StringComparison.Ordinal)
            && new Uri(pair.Key["http-get:".Length..].Split('#')[0]).AbsolutePath == "/stats")
            .Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value)).ToArray();
        if (stats.Length != 10 || Enumerable.Range(0, 5).Any(i => stats[i * 2 + 1].GetProperty("queryRequests").GetInt32() != stats[i * 2].GetProperty("queryRequests").GetInt32() + 1
            || stats[i * 2 + 1].GetProperty("queryReads").GetInt32() != stats[i * 2].GetProperty("queryReads").GetInt32() + (i == 0 ? 1 : 0)))
            throw new InvalidOperationException("The actual handler transport lost its one request and authorized-read accounting.");
    }
}
