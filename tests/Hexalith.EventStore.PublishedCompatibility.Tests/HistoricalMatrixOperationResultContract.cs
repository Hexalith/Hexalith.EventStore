using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Binds existing operation claims and replay outcomes to their actual endpoint/status/response capture.</summary>
internal static class HistoricalMatrixOperationResultContract
{
    internal static void Validate(ScenarioObservation observation, IReadOnlyDictionary<string, string> records)
    {
        var profiles = records.Where(pair => pair.Key.Split('#')[0] == "runtime-profile").Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value)).ToArray();
        var posts = records.Where(pair => pair.Key.Split('#')[0] == "http-post").Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value)).ToArray();
        foreach (var (kind, route) in new[] { ("actual-writer-admission-result", "writer-admission"), ("capability-security-observations", "capabilities") })
        foreach (var record in records.Where(pair => pair.Key.Split('#')[0] == kind))
        {
            var claim = JsonSerializer.Deserialize<JsonElement>(record.Value);
            var matches = posts.Where(post => new Uri(Text(post, "address")).AbsolutePath.StartsWith("/" + route + "/", StringComparison.Ordinal)
                && post.GetProperty("status").GetInt32() == 200
                && JsonElement.DeepEquals(post.GetProperty("response").GetProperty("result"), claim)).ToArray();
            if (matches.Length != 1 || !profiles.Any(profile => Text(matches[0], "address").StartsWith(Text(profile, "hostAddress") + "/" + route + "/", StringComparison.Ordinal)))
                throw new InvalidOperationException("The actual " + kind + " claim differs from its owned operation address/status/response commitment.");
        }
        var executes = records.Where(pair => pair.Key.Split('#')[0] == "actual-execute-result").Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value)).ToArray();
        if (observation.Scenario is "full-replay" or "snapshot-tail" or "retained-covered" or "retained-uncovered" or "missing-event" or "checkout"
            && executes.Length == 0)
            throw new InvalidOperationException("The actual replay/snapshot execution result was not retained.");
        foreach (var execute in executes)
        {
            var profile = profiles.Single(profile => Text(profile, "profileId") == Text(execute, "profileId"));
            string address = Text(profile, "hostAddress") + "/execute/" + Uri.EscapeDataString(Text(execute, "actor"));
            bool append = execute.GetProperty("append").GetBoolean(); var result = execute.GetProperty("result");
            var matches = posts.Where(post => Text(post, "address") == address && post.GetProperty("status").GetInt32() == 200
                && post.GetProperty("request").GetProperty("append").GetBoolean() == append
                && Text(post, "responseHash") == Text(execute, "responseHash")
                && JsonElement.DeepEquals(post.GetProperty("response").GetProperty("result"), result)).ToArray();
            if (matches.Length != 1 || Text(execute, "address") != address || execute.GetProperty("hostPid").GetInt32() != profile.GetProperty("hostProcessId").GetInt32())
                throw new InvalidOperationException("The actual execute claim differs from its owned request/status/response commitment.");
            if (result.GetProperty("ok").GetBoolean())
            {
                long count = result.GetProperty("count").GetInt64(), sequence = result.GetProperty("sequence").GetInt64();
                if (count < 0 || sequence != count + (append ? 1 : 0))
                    throw new InvalidOperationException("The actual execution lost its persisted count/sequence relationship.");
            }
        }
        if (observation.Scenario is "full-replay" or "snapshot-tail" or "retained-covered" or "checkout")
        {
            var reads = executes.Where(execute => !execute.GetProperty("append").GetBoolean()).ToArray();
            if (reads.Length == 0 || reads.Any(execute => !execute.GetProperty("result").GetProperty("ok").GetBoolean()
                || execute.GetProperty("result").GetProperty("count").GetInt64() != 12 || execute.GetProperty("result").GetProperty("sequence").GetInt64() != 12))
                throw new InvalidOperationException("The actual fresh replay/snapshot did not return the original12 persisted events.");
        }
    }

    private static string Text(JsonElement value, string name) => value.GetProperty(name).GetString() ?? throw new InvalidOperationException("An actual operation coordinate is absent.");
}
