using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Binds operation records to independently observed actual POST bytes and safe semantics.</summary>
internal static class HistoricalMatrixTransportContract
{
    internal static void Validate(ScenarioObservation observation, IReadOnlyList<QualificationHttpExchange> exchanges)
    {
        var used = new HashSet<QualificationHttpExchange>();
        foreach (var record in observation.Measurements ?? new Dictionary<string, string>())
        {
            if (record.Key.Split('#')[0] == "http-get-observation")
            {
                var get = JsonSerializer.Deserialize<JsonElement>(record.Value);
                var actual = exchanges.Where(item => item.Method == "GET" && item.Address == get.GetProperty("address").GetString()
                    && item.Started >= get.GetProperty("began").GetDateTimeOffset() && item.Ended <= get.GetProperty("ended").GetDateTimeOffset()
                    && item.Status == get.GetProperty("status").GetInt32() && item.ResponseSha256 == get.GetProperty("bodyHash").GetString()).ToArray();
                if (actual.Length != 1 || !used.Add(actual[0])) throw new InvalidOperationException("The GET record lacks its independent actual transport receipt.");
                continue;
            }
            if (record.Key.Split('#')[0] != "http-post") continue;
            var post = JsonSerializer.Deserialize<JsonElement>(record.Value);
            var matches = exchanges.Where(item => item.Method == "POST" && item.Address == post.GetProperty("address").GetString()
                && item.Started >= post.GetProperty("began").GetDateTimeOffset() && item.Ended <= post.GetProperty("ended").GetDateTimeOffset()
                && item.Status == post.GetProperty("status").GetInt32()
                && item.RequestSha256 == post.GetProperty("requestHash").GetString()
                && item.ResponseSha256 == post.GetProperty("responseHash").GetString()
                && item.RequestSemanticsSha256 == QualificationRuntime.Hash(JsonSerializer.SerializeToUtf8Bytes(post.GetProperty("request")))
                && item.ResponseSemanticsSha256 == QualificationRuntime.Hash(JsonSerializer.SerializeToUtf8Bytes(post.GetProperty("response")))).ToArray();
            if (matches.Length != 1 || !used.Add(matches[0]))
                throw new InvalidOperationException("The POST record lacks its unique independent actual transport byte/semantic commitments: " + observation.Scenario + "/" + observation.Case + "/" + record.Key);
        }
    }
}
