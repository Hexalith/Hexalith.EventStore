using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Retains the original JSON-byte identity when signed fields require a safe persisted projection.</summary>
internal static class HistoricalRecordCommitment
{
    internal static string Hash(IReadOnlyDictionary<string, string> records, string key)
        => records.TryGetValue("record-byte-commitment:" + key, out var captured)
            ? JsonSerializer.Deserialize<JsonElement>(captured).GetProperty("sha256").GetString()!
            : QualificationRuntime.Hash(Encoding.UTF8.GetBytes(records[key]));
}
