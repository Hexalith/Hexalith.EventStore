using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Retains the finite generated operation result fields, excluding unrelated raw extension fields.</summary>
internal static class HistoricalHttpResultCapture
{
    private static readonly HashSet<string> Fields = new(StringComparer.Ordinal)
    {
        "ok", "count", "sequence", "metadata", "error", "message", "deadLetterBookkeeping", "refused", "code", "stage",
        "protectionCalls", "allocations", "staging", "supported", "denial", "stale", "forged", "completedStale", "unauthorized", "forgedEffect",
        "accepted", "replayed", "baseline", "effect", "recoveryFenceReused", "decoded", "success", "position", "watermark", "model", "cursor", "payload", "errorMessage", "router", "invoker",
        "declared", "persisted", "registry", "unknown", "proof", "status", "timestamp", "aggregateId", "eventCount", "messageId", "correlationId",
        "retryable", "recoveryReasonCode", "drainAttemptCount",
    };

    internal static JsonElement Capture(JsonElement result)
    {
        var safe = new JsonObject();
        foreach (var field in result.EnumerateObject().Where(field => Fields.Contains(field.Name)))
            safe[field.Name] = JsonSerializer.SerializeToNode(HistoricalSafeJson.Capture(field.Value));
        // Apply signed-field names at the root as well as inside the retained payload.
        return HistoricalSafeJson.Capture(JsonSerializer.SerializeToElement(safe));
    }
}
