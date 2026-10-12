using System.Text.Json;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Inspects raw snapshot fields before a record is bound or offered to domain logic.</summary>
internal static class SnapshotEnvelopeInspector
{
    /// <summary>Checks whether actor serialization can write the compact version-one envelope.</summary>
    internal static bool IsSupportedWriteSerializer(JsonSerializerOptions options)
    {
        if (options.WriteIndented) { return false; }
        try
        {
            var probe = new SnapshotRecord(1, JsonSerializer.Deserialize<JsonElement>("{}"),
                DateTimeOffset.UnixEpoch, "d", "a", "t") { SnapshotEnvelopeVersion = 1 };
            using var output = new BoundedSnapshotByteStream(4096);
            JsonSerializer.Serialize(output, probe, options);
            using JsonDocument document = JsonDocument.Parse(output.ToArray());
            JsonElement raw = document.RootElement;
            if (raw.ValueKind != JsonValueKind.Object
                || !raw.TryGetProperty("snapshotEnvelopeVersion", out _)
                || !raw.TryGetProperty("sequenceNumber", out _)
                || !raw.TryGetProperty("state", out _)
                || !raw.TryGetProperty("createdAt", out _)
                || !raw.TryGetProperty("domain", out _)
                || !raw.TryGetProperty("aggregateId", out _)
                || !raw.TryGetProperty("tenantId", out _)
                || !TryReadVersion(raw, out int version) || version != 1
                || !TryReadSequence(raw, out long sequence) || sequence != 1)
            {
                return false;
            }
            SnapshotRecord? readback = raw.Deserialize<SnapshotRecord>(JsonSerializerOptions.Web);
            return readback is { SnapshotEnvelopeVersion: 1, SequenceNumber: 1, Domain: "d", AggregateId: "a", TenantId: "t" }
                && readback.State is JsonElement state && state.ValueKind == JsonValueKind.Object;
        }
        catch { return false; }
    }

    /// <summary>Reads a property only when it appears exactly once.</summary>
    internal static bool TryGetUniqueProperty(JsonElement value, string name, out JsonElement property)
    {
        property = default;
        if (value.ValueKind != JsonValueKind.Object) { return false; }

        bool found = false;
        foreach (JsonProperty candidate in value.EnumerateObject())
        {
            if (!string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)) { continue; }
            if (found) { property = default; return false; }
            property = candidate.Value;
            found = true;
        }
        return found;
    }

    /// <summary>Reads a supported raw envelope version before binding the record.</summary>
    internal static bool TryReadVersion(JsonElement raw, out int version)
    {
        version = 0;
        if (raw.ValueKind != JsonValueKind.Object) { return false; }
        int matches = 0;
        foreach (JsonProperty property in raw.EnumerateObject())
        {
            if (!string.Equals(property.Name, "snapshotEnvelopeVersion", StringComparison.OrdinalIgnoreCase)) { continue; }
            if (++matches != 1 || property.Value.ValueKind != JsonValueKind.Number
                || !property.Value.TryGetInt32(out version)) { return false; }
        }
        return version is 0 or 1;
    }

    /// <summary>Reads a unique positive integral covered sequence.</summary>
    internal static bool TryReadSequence(JsonElement raw, out long sequence)
    {
        sequence = 0;
        return TryGetUniqueProperty(raw, "sequenceNumber", out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out sequence)
            && sequence > 0;
    }

    /// <summary>Checks for an object without the legacy command DTO shape or recursive duplicate names.</summary>
    internal static bool IsFoldedObject(JsonElement state)
        => state.ValueKind == JsonValueKind.Object && !IsDomainServiceCurrentState(state)
            && !HasDuplicatePropertyNames(state);

    /// <summary>Rejects case-insensitive duplicate names throughout objects and nested arrays.</summary>
    internal static bool HasDuplicatePropertyNames(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                if (HasDuplicatePropertyNames(item)) { return true; }
            }
            return false;
        }
        if (value.ValueKind != JsonValueKind.Object) { return false; }
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!names.Add(property.Name) || HasDuplicatePropertyNames(property.Value)) { return true; }
        }
        return false;
    }

    /// <summary>Detects the ambiguous top-level legacy command DTO shape.</summary>
    internal static bool IsDomainServiceCurrentState(JsonElement state)
        => state.ValueKind == JsonValueKind.Object
            && state.TryGetProperty("currentSequence", out _)
            && state.TryGetProperty("events", out _);
}
