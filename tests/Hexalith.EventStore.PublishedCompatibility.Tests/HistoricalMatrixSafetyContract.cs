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
            string kind = key.Split('#')[0];
            if (kind == "actual-versioned-reader-result")
            {
                bool selected = observation.Case.StartsWith("3.128.2-", StringComparison.Ordinal);
                bool invalidFloor = observation.Case.Contains("-floor-0-", StringComparison.Ordinal)
                    || observation.Case.Contains("-floor-14-", StringComparison.Ordinal);
                bool invalidVersion = observation.Case.Contains("-version-987-", StringComparison.Ordinal);
                bool refuses = selected && (invalidFloor || invalidVersion);
                if (data.GetProperty("ok").GetBoolean() == refuses
                    || refuses && (Text(data, "error") != "System.InvalidOperationException"
                        || Text(data, "message") != (invalidFloor ? "SourceHeadChanged: actor metadata has an invalid retained floor."
                            : "RollbackReaderCapabilityHold: a typed reader cannot consume unsupported event metadata."))
                    || !refuses && (data.GetProperty("sequence").GetInt64() != 2 || data.GetProperty("count").GetInt32() != 2))
                    throw new InvalidOperationException("The actual reader lost its case-specific refusal or characterized raw-reader behavior.");
            }
            else if (kind == "production-manifest-command-result")
            {
                var refusal = JsonSerializer.Deserialize<JsonElement>(records["production-manifest-refusal"]);
                string actor = Text(refusal.GetProperty("cause"), "actor");
                var profile = records.Where(pair => pair.Key.Split('#')[0] == "runtime-profile").Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value)).Last();
                string expectedAddress = Text(profile, "hostAddress") + "/execute/" + Uri.EscapeDataString(actor);
                var transports = records.Where(pair => pair.Key.Split('#')[0] == "http-post").Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value))
                    .Where(post => Text(post, "address") == expectedAddress && post.GetProperty("status").GetInt32() == 200
                        && Text(post, "responseHash") == QualificationRuntime.Hash(System.Text.Encoding.UTF8.GetBytes(data.GetRawText()))).ToArray();
                if (transports.Length != 1 || transports[0].GetProperty("request").GetProperty("append").ValueKind != JsonValueKind.True
                    || data.GetProperty("ok").GetBoolean() || Text(data, "error") != "CommandProcessingRefusal"
                    || Text(data, "message") != "Protected data diagnostic details were redacted. ReasonCode=logical-event-read-rejected; Stage=Processing."
                    || data.GetProperty("count").GetInt32() != -1 || data.GetProperty("sequence").GetInt64() != 1
                    || data.GetProperty("metadata").GetProperty("currentSequence").GetInt64() != 1
                    || !refusal.GetProperty("observed").GetBoolean()
                    || Text(refusal.GetProperty("cause"), "code") != "missing-allow-listed-callable"
                    || Text(refusal.GetProperty("cause"), "stage") != "production-logical-event-upcast-binding")
                    throw new InvalidOperationException("The actual manifest command lost its precise missing-callable refusal before mutation.");
            }
            else if (kind == "production-handler-discovery")
            {
                if (!data.GetProperty("declared").EnumerateArray().Select(item => item.GetString()).SequenceEqual(["get-counter"])
                    || !JsonElement.DeepEquals(data.GetProperty("declared"), data.GetProperty("persisted"))
                    || !data.GetProperty("supported").GetBoolean() || data.GetProperty("unknown").GetBoolean()
                    || Text(data, "registry") != "Hexalith.EventStore.Queries.DaprDomainQueryHandlerRegistry")
                    throw new InvalidOperationException("The actual production registry lost its exact persisted declared-handler inventory.");
            }
            else if (kind.StartsWith("production-handler-", StringComparison.Ordinal))
            {
                bool valid = kind == "production-handler-valid";
                if (Text(data, "router") != "Hexalith.EventStore.Queries.HandlerAwareQueryRouter"
                    || Text(data, "invoker") != "Hexalith.EventStore.Queries.DaprDomainQueryInvoker"
                    || data.GetProperty("success").GetBoolean() != valid
                    || !valid && Text(data, "errorMessage") != "protected-admission-refused")
                    throw new InvalidOperationException("The production query handler lost its exact router, invoker or protected admission outcome.");
                if (valid)
                {
                    var payload = data.GetProperty("payload");
                    foreach (var (field, expected) in new[] { ("originalActorId", "actor"), ("authenticatedWorkloadId", "workload"),
                        ("delegationId", "delegation"), ("tenantId", "tenant-a"), ("correlationId", "corr") })
                        if (Text(payload, field) != expected) throw new InvalidOperationException("The production handler changed a bound principal coordinate.");
                    if (!payload.GetProperty("isDelegated").GetBoolean() || payload.GetProperty("scopes")[0].GetString() != "counter.read"
                        || payload.GetProperty("audience")[0].GetString() != "eventstore"
                        || !HistoricalSafeJson.NonemptyString(payload.GetProperty("identityAdmissionProof"))
                        || payload.GetProperty("value").GetProperty("watermark").GetInt64() != 987)
                        throw new InvalidOperationException("The production handler lost its actual proof, authority or durable query result.");
                }
            }
            else if (kind == "actual-writer-admission-result")
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
