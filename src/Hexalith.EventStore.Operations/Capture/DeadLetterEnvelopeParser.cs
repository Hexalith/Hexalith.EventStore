using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Operations.Models;

namespace Hexalith.EventStore.Operations.Capture;

/// <summary>
/// Extracts replay-safe identity from a structured CloudEvent without materializing its payload contract.
/// </summary>
internal static class DeadLetterEnvelopeParser
{
    /// <summary>Parses a raw body and returns its safe identity and stable hash.</summary>
    internal static (DeadLetterSafeIdentity Identity, string BodySha256) Parse(ReadOnlySpan<byte> body)
    {
        string bodySha256 = Convert.ToHexStringLower(SHA256.HashData(body));
        string fallbackMessageId = "unidentified-" + bodySha256[..24];

        try
        {
            using JsonDocument document = JsonDocument.Parse(body.ToArray());
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !TryGetUniqueString(root, "specversion", out string? specVersion)
                || !string.Equals(specVersion, "1.0", StringComparison.Ordinal)
                || !TryGetUniqueString(root, "id", out string? cloudEventId)
                || !TryGetUniqueString(root, "source", out _)
                || !TryGetUniqueString(root, "type", out _)
                || !TryGetUniqueObject(root, "data", out JsonElement data)
                || !TryGetUniqueString(data, "messageId", out string? eventStoreMessageId)
                || !string.Equals(cloudEventId, eventStoreMessageId, StringComparison.Ordinal)
                || !TryGetUniqueString(data, "tenantId", out string? tenantId)
                || !TryGetUniqueString(data, "domain", out string? domain)
                || !TryGetUniqueString(data, "aggregateId", out string? aggregateId)
                || !TryGetUniqueString(data, "correlationId", out string? correlationId)
                || !TryGetUniqueAliasString(data, out string? eventType, "eventTypeName", "eventName", "eventType"))
            {
                return (new DeadLetterSafeIdentity(fallbackMessageId, null, null, null, null, null), bodySha256);
            }

            return (new DeadLetterSafeIdentity(
                cloudEventId!,
                tenantId,
                domain,
                aggregateId,
                correlationId,
                eventType), bodySha256);
        }
        catch (JsonException)
        {
            return (new DeadLetterSafeIdentity(fallbackMessageId, null, null, null, null, null), bodySha256);
        }
    }

    private static bool TryGetUniqueObject(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        int matches = 0;
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                matches++;
                value = property.Value;
            }
        }

        return matches == 1 && value.ValueKind == JsonValueKind.Object;
    }

    /// <summary>
    /// Reads the single event-type property, accepting every name the EventStore publisher contract uses.
    /// </summary>
    /// <remarks>
    /// <c>eventTypeName</c> is the name the publisher actually emits: the published <c>data</c> object is a
    /// serialized <c>EventEnvelope</c> / <c>EventStoreDomainEventEnvelope</c>, whose event-type member is
    /// <c>EventTypeName</c>. The shorter aliases are retained for envelopes produced by other publishers.
    /// More than one match stays ambiguous and therefore replay-ineligible.
    /// </remarks>
    private static bool TryGetUniqueAliasString(
        JsonElement element,
        out string? value,
        params string[] names)
    {
        value = null;
        int matches = 0;
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (names.Any(name => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                matches++;
                value = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
            }
        }

        return matches == 1 && DeadLetterSafeIdentity.IsValidValue(value);
    }

    private static bool TryGetUniqueString(JsonElement element, string name, out string? value)
    {
        value = null;
        int matches = 0;
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                matches++;
                value = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
            }
        }

        return matches == 1 && DeadLetterSafeIdentity.IsValidValue(value);
    }
}
