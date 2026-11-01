using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Binds actual opposite-version clients, actor effects and security refusals to their executed probe operations.</summary>
internal static class HistoricalMatrixCrossClientContract
{
    internal static void Validate(ScenarioObservation observation, IReadOnlyDictionary<string, string> records,
        IReadOnlyList<ConsumerBuild> builds, IReadOnlyList<HistoricalExecutionCommand> commands)
    {
        var measurements = records.Where(pair => pair.Key.StartsWith("cross-client-", StringComparison.Ordinal)).ToArray();
        if (measurements.Length == 0) return;
        string expectedClient = HistoricalMatrixScenarioContract.Probe(observation, 0).Build;
        ConsumerBuild client = builds.Single(build => HistoricalMatrixScenarioContract.Key(build) == expectedClient);
        RequireInventories(observation, records);
        bool unavailableClient = client.Version == "3.70.1", unavailableHost = observation.Case.Contains("-to-host-3.70.1-", StringComparison.Ordinal);
        foreach (var measured in measurements)
        {
            string action = measured.Key["cross-client-".Length..].Split('#')[0];
            using var document = JsonDocument.Parse(measured.Value); var data = document.RootElement;
            HistoricalMatrixEvidenceContract.ValidateIdentities(data, client);
            if (data.GetProperty("action").GetString() != action) throw new InvalidOperationException("The actor-client action differs from its actual operation.");
            var captures = commands.Where(command => command.Operation == "cross-client" && command.Action == action
                && command.Record.GetProperty("Started").GetDateTimeOffset() >= observation.Started
                && command.Record.GetProperty("Ended").GetDateTimeOffset() <= observation.Ended
                && command.Record.GetProperty("Command").GetProperty("Arguments")[0].GetString() == client.Assembly
                && command.Record.GetProperty("ExitCode").GetInt32() == 0).ToArray();
            if (captures.Length != 1 || !JsonElement.DeepEquals(data, PublishedScenarioTests.ParseProbeOutput(captures[0].Record.GetProperty("StandardOutput").GetString()!)))
                throw new InvalidOperationException("The cross-client measurement has no unique actual matching client/result.");
            bool ok = data.GetProperty("ok").GetBoolean(), supported = data.GetProperty("supported").GetBoolean();
            if (action == "common")
            {
                var outcome = data.GetProperty("outcome");
                if (!ok || !supported || !outcome.GetProperty("accepted").GetBoolean() || outcome.GetProperty("before").GetInt64() != 0
                    || outcome.GetProperty("sequence").GetInt64() != 1) throw new InvalidOperationException("The actual common actor append was erased.");
            }
            else if (unavailableClient)
            {
                if (ok || supported || data.GetProperty("code").GetString() != "client-contract-lacks-protected-methods"
                    || !data.GetProperty("missingMethods").EnumerateArray().Select(item => item.GetString()).Order(StringComparer.Ordinal)
                        .SequenceEqual(new[] { "ProcessFencedCommandAsync", "ProcessTrustedEffectAsync" }))
                    throw new InvalidOperationException("The historical client-contract absence was erased.");
            }
            else if (unavailableHost)
            {
                string request = action.StartsWith("fenced", StringComparison.Ordinal) ? "ProcessFencedCommandAsyncReqBody" : "ProcessTrustedEffectAsyncReqBody";
                if (ok || !supported || data.GetProperty("actualType").GetString() != "System.Runtime.Serialization.SerializationException"
                    || !Text(data, "message").Contains("data of the 'urn:actors:" + request + "' data contract.", StringComparison.Ordinal)
                    || !Text(data, "message").Contains("The deserializer has no knowledge of any type that maps to this contract.", StringComparison.Ordinal)
                    || !Text(data, "cause").Contains(Text(data, "message"), StringComparison.Ordinal))
                    throw new InvalidOperationException("The historical host remoting-contract refusal was erased.");
            }
            else if (action.EndsWith("forged", StringComparison.Ordinal))
            {
                string message = action == "fenced-forged" ? "The idempotency execution fence is missing, stale, or invalid." : "Trusted effect gateway proof is invalid.";
                if (ok || !supported || Text(data, "actualType") != "System.InvalidOperationException" || Text(data, "message") != message
                    || !Text(data, "cause").Contains(message, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(Text(data, "error")))
                    throw new InvalidOperationException("The forged actor request lost its exact causal security refusal.");
            }
            else
            {
                var outcome = data.GetProperty("outcome");
                if (!ok || !supported || !outcome.GetProperty("accepted").GetBoolean()
                    || outcome.GetProperty("sequence").GetInt64() != (action == "fenced-valid" ? 2 : 3)
                    || action == "trusted-replay" && !outcome.GetProperty("replayed").GetBoolean())
                    throw new InvalidOperationException("The actual protected actor append/replay was erased.");
            }
        }
    }

    private static void RequireInventories(ScenarioObservation observation, IReadOnlyDictionary<string, string> records)
    {
        var inventories = records.Where(pair => pair.Key.StartsWith("inventory:", StringComparison.Ordinal))
            .OrderBy(pair => HistoricalMatrixScenarioContract.Ordinal(pair.Key)).Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value)).ToArray();
        bool absent = observation.Case.Contains("3.70.1", StringComparison.Ordinal);
        if (inventories.Length != (absent ? 3 : 13) || inventories.Select(value => Text(value, "fixture")).Distinct(StringComparer.Ordinal).Count() != 1)
            throw new InvalidOperationException("The cross-client operation lost its complete actual before/after inventory geometry.");
        string actor = Text(inventories[0], "fixture");
        var baseline = inventories[0].GetProperty("hashes");
        string[] keys = [actor + ":metadata", actor + ":snapshot", .. Enumerable.Range(1, 4).Select(sequence => actor + ":events:" + sequence)];
        for (int i = 0; i < inventories.Length; i++)
        {
            var hashes = inventories[i].GetProperty("hashes"); int head = absent || i < 4 ? 1 : i < 8 ? 2 : 3;
            if (!hashes.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).SequenceEqual(keys.Order(StringComparer.Ordinal))
                || !JsonElement.DeepEquals(hashes.GetProperty(actor + ":snapshot"), baseline.GetProperty(actor + ":snapshot")))
                throw new InvalidOperationException("The cross-client actor changed its exact state-key or snapshot inventory.");
            foreach (string key in keys.Where(key => key.EndsWith(":metadata", StringComparison.Ordinal)
                || Enumerable.Range(1, head).Any(sequence => key == actor + ":events:" + sequence)))
                if (hashes.GetProperty(key).GetString() is not { Length: 64 } hash || !hash.All(char.IsAsciiHexDigitLower))
                    throw new InvalidOperationException("The cross-client actor lost actual committed metadata or a required event.");
            for (int sequence = head + 1; sequence <= 4; sequence++)
                if (hashes.GetProperty(actor + ":events:" + sequence).ValueKind != JsonValueKind.Null)
                    throw new InvalidOperationException("A protected refusal/replay introduced an unadmitted event.");
            if (i == 0) continue;
            var previous = inventories[i - 1].GetProperty("hashes");
            bool append = !absent && i is 4 or 8;
            if (!append && !JsonElement.DeepEquals(hashes, previous))
                throw new InvalidOperationException("The protected refusal/replay/restart changed committed bytes.");
            if (append)
            {
                if (JsonElement.DeepEquals(hashes.GetProperty(actor + ":metadata"), previous.GetProperty(actor + ":metadata")))
                    throw new InvalidOperationException("The admitted append lost its changed metadata sequence.");
                for (int sequence = 1; sequence < head; sequence++)
                    if (!JsonElement.DeepEquals(hashes.GetProperty(actor + ":events:" + sequence), previous.GetProperty(actor + ":events:" + sequence)))
                        throw new InvalidOperationException("The admitted append changed an original event.");
            }
        }
    }

    private static string Text(JsonElement data, string name) => data.GetProperty(name).GetString()
        ?? throw new InvalidOperationException("The actual cross-client refusal coordinate is absent.");
}
