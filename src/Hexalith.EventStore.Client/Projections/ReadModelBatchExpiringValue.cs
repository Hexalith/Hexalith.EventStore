using System.Text.Json;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Preserves an inclusive source deadline after batch compaction and provider TTL rounding.</summary>
internal static class ReadModelBatchExpiringValue
{
    private const string Discriminator = "$hxrme";

    /// <summary>Identifies a platform-owned deadline wrapper, including malformed metadata that must fail closed.</summary>
    internal static bool IsExpiring(ReadOnlySpan<byte> bytes)
    {
        var reader = new Utf8JsonReader(bytes);
        while (reader.Read())
            if (reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == 1
                && reader.ValueTextEquals(Discriminator)) return true;
        return false;
    }

    /// <summary>Encodes the same immutable deadline and canonical model bytes.</summary>
    internal static ReadOnlyMemory<byte> Encode(ReadOnlyMemory<byte> value, DateTimeOffset deadline)
        => ReadModelBatchCanonicalJson.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [Discriminator] = 1,
            ["expires"] = deadline.UtcTicks,
            ["val"] = Convert.ToBase64String(value.Span),
        });

    /// <summary>Validates the complete wrapper without mistaking invalid evidence for absence.</summary>
    internal static (DateTimeOffset Deadline, ReadOnlyMemory<byte> Value) Decode(ReadOnlyMemory<byte> bytes)
    {
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        string[] names = [Discriminator, "expires", "val"];
        JsonProperty[] properties = root.EnumerateObject().ToArray();
        if (properties.Length != names.Length || names.Any(name => properties.Count(property => property.Name == name) != 1)
            || root.GetProperty(Discriminator).GetInt32() != 1) throw new JsonException("Invalid read-model expiry evidence.");
        var deadline = new DateTimeOffset(root.GetProperty("expires").GetInt64(), TimeSpan.Zero);
        ReadOnlyMemory<byte> value = root.GetProperty("val").GetBytesFromBase64();
        using JsonDocument model = JsonDocument.Parse(value);
        if (ReadModelBatchEnvelope.IsEnvelope(value.Span) || IsExpiring(value.Span))
            throw new JsonException("Nested read-model expiry evidence.");
        return (deadline, value);
    }

    /// <summary>Returns the logical value, preserving its physical CAS token while it remains readable.</summary>
    internal static RawStateEntry Resolve(RawStateEntry entry, TimeProvider clock)
    {
        if (!entry.Exists || !IsExpiring(entry.Value.Span)) return entry;
        (DateTimeOffset deadline, ReadOnlyMemory<byte> value) = Decode(entry.Value);
        return deadline <= clock.GetUtcNow()
            ? new RawStateEntry(false, ReadOnlyMemory<byte>.Empty, string.Empty)
            : new RawStateEntry(true, value, entry.ETag);
    }

    /// <summary>Computes retention from the original deadline at the last responsible moment.</summary>
    internal static TimeSpan? Retention(DateTimeOffset? deadline, TimeSpan? relative, TimeProvider clock)
        => deadline is { } expires ? expires - clock.GetUtcNow() : relative;

    /// <summary>Restores previous final bytes without discarding their original retention.</summary>
    internal static async Task<bool> RestoreAsync(IReadModelBatchStateAccessor accessor, string key,
        ReadOnlyMemory<byte> bytes, string etag, TimeProvider clock, CancellationToken token)
    {
        TimeSpan? retention = IsExpiring(bytes.Span) ? Decode(bytes).Deadline - clock.GetUtcNow() : null;
        return retention <= TimeSpan.Zero
            ? await accessor.TryDeleteAsync(key, etag, token).ConfigureAwait(false)
            : await accessor.TryWriteAsync(key, bytes, etag, retention, token).ConfigureAwait(false);
    }
}
