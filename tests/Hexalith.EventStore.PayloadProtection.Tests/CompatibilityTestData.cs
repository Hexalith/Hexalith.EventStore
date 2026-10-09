using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>
/// Builds Story 8.4 stored records over the G-001 helpers and scans results for a no-leak sentinel.
/// </summary>
internal static class CompatibilityTestData
{
    /// <summary>Gets the persisted G-001 event type name.</summary>
    internal const string EventTypeName = "Hexalith.Parties.Contracts.Events.PartyCreated";

    /// <summary>Gets the registered current snapshot type identifier.</summary>
    internal const string SnapshotTypeId = "hx-snapshot-v1:party-state";

    /// <summary>Gets the registered historical snapshot type alias.</summary>
    internal const string SnapshotAlias = "hx-snapshot-v1:party-state-v0";

    /// <summary>Gets a unique sentinel that must never appear in an unreadable result or exception.</summary>
    internal const string Sentinel = "zq8q4-canary-7d1f";

    /// <summary>Gets the G-001 aggregate identity.</summary>
    internal static AggregateIdentity Identity { get; } = new("tenant-a", "parties", "party-01");

    /// <summary>Creates exact section 8.4 v2 metadata.</summary>
    internal static EventStorePayloadProtectionMetadata V2Metadata()
        => new(
            PayloadProtectionState.Protected,
            1,
            PayloadCompatibilityClassifier.SharedV2Scheme,
            null,
            PayloadCompatibilityClassifier.V2ContentHint,
            Flags(("format", "json+pdenc-v2"), ("envelope", "pdenc-v2")));

    /// <summary>Creates exact Appendix B Parties v1 metadata.</summary>
    internal static EventStorePayloadProtectionMetadata PartiesV1Metadata()
        => new(
            PayloadProtectionState.Protected,
            1,
            PayloadCompatibilityClassifier.PartiesV1Scheme,
            null,
            "application/json",
            Flags(("format", "json+pdenc-v1"), ("field-envelope", "pdenc-v1")));

    /// <summary>Creates an ordinal read-only flag dictionary.</summary>
    internal static IReadOnlyDictionary<string, string> Flags(params (string Key, string Value)[] entries)
        => new ReadOnlyDictionary<string, string>(entries.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Value,
            StringComparer.Ordinal));

    /// <summary>Serializes metadata through the existing carrier.</summary>
    internal static string Carrier(EventStorePayloadProtectionMetadata metadata)
        => EventStorePayloadProtectionMetadataCarrier.Serialize(metadata);

    /// <summary>Creates the exact v2 carrier text.</summary>
    internal static string V2Carrier() => Carrier(V2Metadata());

    /// <summary>Creates the exact Parties v1 carrier text.</summary>
    internal static string V1Carrier() => Carrier(PartiesV1Metadata());

    /// <summary>Creates the exact unprotected carrier text.</summary>
    internal static string UnprotectedCarrier() => Carrier(EventStorePayloadProtectionMetadata.Unprotected());

    /// <summary>Resolves a carrier kind used by table-driven tests.</summary>
    internal static string? CarrierFor(string kind) => kind switch
    {
        "none" => null,
        "unprotected" => UnprotectedCarrier(),
        "v1" => V1Carrier(),
        "v2" => V2Carrier(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Creates fresh plaintext event JSON.</summary>
    internal static byte[] PlainJson() => "{\"email\":\"alice@example.com\",\"name\":\"Alice\"}"u8.ToArray();

    /// <summary>Creates fresh redacted event JSON.</summary>
    internal static byte[] RedactedJson() => "{\"email\":\"[redacted]\",\"name\":\"Alice\"}"u8.ToArray();

    /// <summary>Creates a bounded Parties-shaped v1 field marker payload; only its shape matters to the router.</summary>
    internal static byte[] V1Json()
        => "{\"email\":{\"$enc\":{\"alg\":\"A256GCM\",\"kv\":1,\"n\":\"bm9uY2U\",\"t\":\"dGFn\",\"c\":\"Y2lwaGVy\"}},\"name\":\"Alice\"}"u8.ToArray();

    /// <summary>Protects the plaintext event at one sequence through the unchanged Story 8.3 core.</summary>
    internal static byte[] V2Json(ulong sequence = 1, string email = "alice@example.com")
        => new PayloadProtectionCore().ProtectEvent(
            Encoding.UTF8.GetBytes("{\"email\":\"" + email + "\",\"name\":\"Alice\"}"),
            ["/email"],
            TestFixture.Context(sequence),
            TestFixture.Material).PayloadBytes;

    /// <summary>Resolves a payload kind used by table-driven tests.</summary>
    internal static byte[] PayloadFor(string kind) => kind switch
    {
        "plain" => PlainJson(),
        "v1" => V1Json(),
        "v2" => V2Json(),
        "both" => Encoding.UTF8.GetBytes(
            "{\"a\":" + Encoding.UTF8.GetString(V2Json()) + ",\"b\":" + Encoding.UTF8.GetString(V1Json()) + "}"),
        "invalid" => "{\"email\":"u8.ToArray(),
        "duplicate" => "{\"name\":\"Alice\",\"name\":\"Bob\"}"u8.ToArray(),
        "binary" => [0xff, 0x00, 0xfe, 0x01],
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Creates one stored event record.</summary>
    internal static CompatibilityEventRecord Event(
        ulong sequence,
        byte[] payload,
        string format,
        string? carrier,
        AggregateIdentity? identity = null)
        => new(identity ?? Identity, sequence, EventTypeName, payload, format, carrier);

    /// <summary>Creates a stored legacy event with no carrier.</summary>
    internal static CompatibilityEventRecord LegacyEvent(ulong sequence) => Event(sequence, PlainJson(), "json", null);

    /// <summary>Creates a stored explicitly unprotected event.</summary>
    internal static CompatibilityEventRecord UnprotectedEvent(ulong sequence)
        => Event(sequence, PlainJson(), "json", UnprotectedCarrier());

    /// <summary>Creates a stored redacted event.</summary>
    internal static CompatibilityEventRecord RedactedEvent(ulong sequence)
        => Event(sequence, RedactedJson(), "json-redacted", null);

    /// <summary>Creates a stored custom-format event.</summary>
    internal static CompatibilityEventRecord CustomEvent(ulong sequence)
        => Event(sequence, PayloadFor("binary"), "application/x-protobuf", null);

    /// <summary>Creates a stored Parties v1 event.</summary>
    internal static CompatibilityEventRecord V1Event(ulong sequence, bool withMetadata = true)
        => Event(sequence, V1Json(), "json+pdenc-v1", withMetadata ? V1Carrier() : null);

    /// <summary>Creates a stored v2 event authenticated at its own sequence.</summary>
    internal static CompatibilityEventRecord V2Event(ulong sequence)
        => Event(sequence, V2Json(sequence), "json+pdenc-v2", V2Carrier());

    /// <summary>Creates a v2 event whose stored sequence differs from its authenticated sequence.</summary>
    internal static CompatibilityEventRecord MisplacedV2Event(ulong sequence)
        => Event(sequence, V2Json(sequence + 100), "json+pdenc-v2", V2Carrier());

    /// <summary>Creates fresh plaintext snapshot JSON for the registered state.</summary>
    internal static byte[] SnapshotPlaintext() => "{\"Name\":\"Alice\",\"Version\":3}"u8.ToArray();

    /// <summary>Protects one snapshot through the unchanged Story 8.3 core.</summary>
    internal static ProtectedSnapshotPayloadV2 ProtectSnapshot(
        string snapshotTypeId = SnapshotTypeId,
        byte[]? plaintext = null,
        ulong sequence = 1)
        => new PayloadProtectionCore().ProtectSnapshot(
            plaintext ?? SnapshotPlaintext(),
            new PayloadProtectionContext(Identity, snapshotTypeId, PayloadProtectionPayloadKind.Snapshot, sequence),
            TestFixture.Material);

    /// <summary>Renders a v2 carrier as the equivalent stored JSON element.</summary>
    internal static JsonElement ToElement(ProtectedSnapshotPayloadV2 carrier)
        => Json("{\"Format\":\"" + carrier.Format
            + "\",\"SnapshotTypeId\":\"" + carrier.SnapshotTypeId
            + "\",\"Envelope\":\"" + carrier.Envelope + "\"}");

    /// <summary>Parses detached JSON element state, allowing depth beyond the core bound so tests can exceed it.</summary>
    internal static JsonElement Json(string text)
    {
        using JsonDocument document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 256 });
        return document.RootElement.Clone();
    }

    /// <summary>Creates the registry with one current identifier and one historical alias.</summary>
    internal static SnapshotTypeRegistry Registry()
        => new([new SnapshotTypeRegistration(
            SnapshotTypeId,
            CompatibilityTestJsonContext.Default.PartySnapshotState,
            [SnapshotAlias])]);

    /// <summary>Creates one stored snapshot record.</summary>
    internal static CompatibilitySnapshotRecord Snapshot(
        object state,
        EventStorePayloadProtectionMetadata? metadata,
        ulong sequence = 1)
        => new(Identity, sequence, state, metadata);

    /// <summary>
    /// Asserts that no structured or rendered form of a value contains the sentinel.
    /// </summary>
    internal static void ShouldNotLeak(object? value, string sentinel = Sentinel)
    {
        byte[] sentinelBytes = Encoding.UTF8.GetBytes(sentinel);
        string hex = Convert.ToHexString(sentinelBytes);
        string base64 = Convert.ToBase64String(sentinelBytes);
        foreach (string rendered in Render(value, 0))
        {
            rendered.ShouldNotContain(sentinel, Case.Insensitive);
            rendered.ShouldNotContain(hex, Case.Insensitive);
            rendered.ShouldNotContain(base64, Case.Sensitive);
        }
    }

    private static IEnumerable<string> Render(object? value, int depth)
    {
        if (value is null || depth > 4 || value is Type or MemberInfo)
        {
            yield break;
        }

        switch (value)
        {
            case string text:
                yield return text;
                yield break;
            case byte[] bytes:
                yield return Encoding.UTF8.GetString(bytes);
                yield return Convert.ToHexString(bytes);
                yield return Convert.ToBase64String(bytes);
                yield break;
            case JsonElement element:
                yield return element.ValueKind == JsonValueKind.Undefined ? string.Empty : element.GetRawText();
                yield break;
            case Exception exception:
                yield return exception.ToString();
                foreach (DictionaryEntry entry in exception.Data)
                {
                    yield return entry.Key?.ToString() ?? string.Empty;
                    yield return entry.Value?.ToString() ?? string.Empty;
                }

                yield break;
        }

        yield return value.ToString() ?? string.Empty;
        if (value is Enum || value.GetType().IsPrimitive)
        {
            yield break;
        }

        if (value is IEnumerable sequence)
        {
            foreach (object? item in sequence)
            {
                foreach (string rendered in Render(item, depth + 1))
                {
                    yield return rendered;
                }
            }

            yield break;
        }

        foreach (PropertyInfo property in value.GetType().GetProperties(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            foreach (string rendered in Render(property.GetValue(value), depth + 1))
            {
                yield return rendered;
            }
        }
    }
}
