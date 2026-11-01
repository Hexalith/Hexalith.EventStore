using System.Globalization;
using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Binds actual unique structured stdout to each probe's independent prepared consumer and finite numeric case.</summary>
internal static class HistoricalMatrixProbeContract
{
    internal static void Validate(ScenarioObservation observation, IReadOnlyDictionary<string, string> records,
        ConsumerBuild build, HistoricalExecutionCommand command)
    {
        JsonElement result = PublishedScenarioTests.ParseProbeOutput(command.Record.GetProperty("StandardOutput").GetString()!);
        HistoricalMatrixEvidenceContract.ValidateIdentities(result, build);
        if (command.Operation is "metadata" or "legacy-constructor")
        {
            bool metadataCase = observation.Scenario == "metadata-read";
            long? suppliedFloor = metadataCase ? long.Parse(observation.Case.Split('-')[^1], CultureInfo.InvariantCulture) : null;
            var expected = new HistoricalMetadataInput(12, suppliedFloor, observation.Case.EndsWith("-True", StringComparison.Ordinal),
                QualificationRuntime.Hash(System.Text.Encoding.UTF8.GetBytes("old")), DateTimeOffset.UnixEpoch.UtcTicks);
            if (command.Operation == "metadata" && command.Metadata != expected)
                throw new InvalidOperationException("The captured metadata command did not execute this exact head, floor and casing case.");
            var value = result.GetProperty("value");
            bool hasFloor = value.TryGetProperty("retainedFloor", out var floor);
            if (value.GetProperty("currentSequence").GetInt64() != 12 || value.GetProperty("eTag").GetString() != "old"
                || value.GetProperty("lastModified").GetDateTimeOffset() != DateTimeOffset.UnixEpoch
                || hasFloor != (build.Version != "3.70.1") || hasFloor && floor.GetInt64() != (suppliedFloor ?? 1))
                throw new InvalidOperationException("The actual metadata output lost the exact measured value or characterized old floor absence.");
            if (command.Operation == "legacy-constructor" && !JsonElement.DeepEquals(value,
                JsonSerializer.Deserialize<JsonElement>(records["constructed-metadata"])))
                throw new InvalidOperationException("The recorded constructor result differs from the actual unique structured output.");
        }
        else if (command.Operation == "signed-query")
        {
            foreach (string field in new[] { "verified", "principalsBound", "missingRefused", "forgedRefused", "foreignTenantRefused" })
                if (!result.GetProperty(field).GetBoolean()) throw new InvalidOperationException("The real signed-query producer lost its admission or principal/refusal check.");
            var actual = result.GetProperty("value");
            var expected = HistoricalMatrixWireContract.ReadFixture().GetProperty("querySyntheticSource");
            if (!actual.EnumerateObject().Select(field => field.Name).Order(StringComparer.Ordinal).SequenceEqual(expected.EnumerateObject().Select(field => field.Name).Order(StringComparer.Ordinal))
                || expected.EnumerateObject().Where(field => field.Name != "identityAdmissionProof").Any(field => HistoricalWireInput.HashValue(field.Value) != HistoricalWireInput.HashValue(actual.GetProperty(field.Name))))
                throw new InvalidOperationException("The real signed-query result changed its exact actual bound scope or principal fields.");
            if (!HistoricalSafeJson.NonemptyString(result.GetProperty("value").GetProperty("identityAdmissionProof")))
                throw new InvalidOperationException("The real signed-query producer lost its actual nonempty proof.");
        }
        else if (command.Operation == "cursor")
        {
            if (build.Version == "3.70.1")
            {
                if (result.GetProperty("supported").GetBoolean() || result.GetProperty("denial").GetString() != "incapable-published-endpoint"
                    || new[] { "protectedAccess", "effect", "write" }.Any(field => result.GetProperty(field).GetBoolean()))
                    throw new InvalidOperationException("The old cursor endpoint lost its exact typed incapable boundary.");
            }
            else if (!result.GetProperty("supported").GetBoolean() || new[] { "decoded", "changed", "foreignCaller", "foreignTenant", "foreignDomain", "changedFilter", "changedQuery", "tamperDenied" }.Any(field => !result.GetProperty(field).GetBoolean())
                || result.GetProperty("position").GetString() != "987" || result.GetProperty("unknownDenied").GetInt32() != 3)
                throw new InvalidOperationException("The actual supported cursor codec lost its exact scope, watermark or refusal checks.");
        }
        else if (command.Operation is "wire" or "make-wire")
        {
            if (!HistoricalSafeJson.NonemptyString(result.GetProperty("wire"))
                || result.GetProperty("value").ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("The measured wire command lost its actual serialized and parsed payload.");
        }
    }
}
