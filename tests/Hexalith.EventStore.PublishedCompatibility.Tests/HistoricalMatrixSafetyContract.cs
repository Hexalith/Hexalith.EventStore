using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Rechecks concrete persisted-state, refusal and timing relationships of the actual historical producers.</summary>
internal static class HistoricalMatrixSafetyContract
{
    internal static void Validate(ScenarioObservation observation, IReadOnlyDictionary<string, string> records)
    {
        if (observation.Scenario is "full-replay" or "snapshot-tail" or "checkout")
        {
            var inventories = records.Where(pair => pair.Key.StartsWith("inventory:", StringComparison.Ordinal))
                .Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value)).ToArray();
            foreach (var group in inventories.GroupBy(item => Text(item, "fixture")))
            {
                var baseline = group.First().GetProperty("hashes");
                RequireCommitted(baseline, group.Key);
                if (group.Count() < 2 || group.Any(item => !JsonElement.DeepEquals(item.GetProperty("hashes"), baseline)))
                    throw new InvalidOperationException("Read-only historical replay must preserve every committed event and metadata byte.");
            }
        }
        foreach ((string key, string raw) in records)
        {
            using var document = JsonDocument.Parse(raw);
            JsonElement data = document.RootElement;
            if (key.Split('#')[0] == "actual-writer-admission-result")
            {
                bool selected = observation.Case.StartsWith("3.128.2-", StringComparison.Ordinal);
                if (data.GetProperty("refused").GetBoolean() != selected
                    || Text(data, "code") != (selected ? "SourceHeadChanged" : "unexpected-fault")
                    || Text(data, "stage") != (selected ? "event-persister-metadata-floor-admission" : "unverified")
                    || selected && (Text(data, "error") != "System.InvalidOperationException"
                        || Text(data, "message") != "SourceHeadChanged: actor metadata has an invalid retained floor.")
                    || new[] { "protectionCalls", "allocations", "staging" }.Any(field => data.GetProperty(field).GetInt32() != (selected ? 0 : 1)))
                    throw new InvalidOperationException("The measured malformed writer boundary lost its exact refusal or characterized unsafe effects.");
            }
            else if (key.Split('#')[0] == "capability-security-observations")
            {
                foreach (var expected in new[]
                {
                    ("stale", "stale-execution-authority", "execution-authority", "The idempotency execution authority is no longer current."),
                    ("forged", "invalid-execution-fence", "execution-fence-proof", "The idempotency execution fence is missing, stale, or invalid."),
                    ("completedStale", "completed-execution-authority", "execution-authority", "The idempotency execution authority is no longer current."),
                    ("unauthorized", "unauthorized-effect-delegation", "trusted-effect-delegation", "Synthetic trusted-effect delegation refused."),
                    ("forgedEffect", "invalid-effect-gateway-proof", "trusted-effect-gateway-proof", "Trusted effect gateway proof is invalid."),
                })
                {
                    var refusal = data.GetProperty(expected.Item1); var before = refusal.GetProperty("before"); var after = refusal.GetProperty("after");
                    string actor = before.EnumerateObject().Select(property => property.Name).Single(name => name.EndsWith(":metadata", StringComparison.Ordinal))[..^":metadata".Length];
                    RequireCommitted(before, actor);
                    var inventory = records.Where(pair => pair.Key.StartsWith("inventory:", StringComparison.Ordinal))
                        .Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value))
                        .FirstOrDefault(item => Text(item, "fixture") == actor);
                    if (!refusal.GetProperty("expected").GetBoolean() || Text(refusal, "actualType") != "System.InvalidOperationException"
                        || Text(refusal, "code") != expected.Item2 || Text(refusal, "stage") != expected.Item3 || Text(refusal, "message") != expected.Item4
                        || inventory.ValueKind == JsonValueKind.Undefined
                        || !before.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
                            .SequenceEqual(inventory.GetProperty("hashes").EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal))
                        || !JsonElement.DeepEquals(before, after))
                        throw new InvalidOperationException("The protected refusal lost exact authority coordinates or complete unchanged committed state.");
                }
                if (!data.GetProperty("accepted").GetBoolean() || !data.GetProperty("replayed").GetBoolean()
                    || data.GetProperty("baseline").GetInt64() != 1 || data.GetProperty("sequence").GetInt64() != 3)
                    throw new InvalidOperationException("The capability proof lost its actual accepted and replayed effects.");
            }
            else if (key.Split('#')[0] == "bounded-reminder-registration-restart-window")
            {
                var began = data.GetProperty("preparationStarted").GetDateTimeOffset();
                var ended = data.GetProperty("preparationFinished").GetDateTimeOffset();
                var due = data.GetProperty("due").GetDateTimeOffset();
                double budget = data.GetProperty("preparationBudgetSeconds").GetDouble(), margin = data.GetProperty("naturalDeliveryMarginSeconds").GetDouble();
                if (budget != 60 || margin != 20 || began < observation.Started || ended > observation.Ended
                    || ended < began || ended - began > TimeSpan.FromSeconds(budget) || ended >= due
                    || due != began.AddSeconds(budget + margin))
                    throw new InvalidOperationException("The natural reminder lost its actual bounded pre-due restart ordering.");
            }
        }
    }

    private static void RequireCommitted(JsonElement hashes, string actor)
    {
        foreach (string key in new[] { actor + ":metadata", actor + ":events:1" })
            if (!hashes.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.String
                || value.GetString() is not { Length: 64 } hash || !hash.All(char.IsAsciiHexDigitLower))
                throw new InvalidOperationException("The measurement lost non-null committed metadata or original events.");
    }

    private static string Text(JsonElement value, string key) => value.GetProperty(key).GetString()
        ?? throw new InvalidOperationException("The measured safety coordinate is absent.");
}
