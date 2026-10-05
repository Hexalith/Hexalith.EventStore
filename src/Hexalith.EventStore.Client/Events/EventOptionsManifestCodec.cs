using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Admits canonical LF options and hashes the expanded implementation-owned schema defaults.</summary>
/// <remarks>This codec cannot attest that an executing component uses the declared runtime settings.</remarks>
internal static class EventOptionsManifestCodec
{
    /// <summary>Checks exact supplied text and schema, then hashes the expanded canonical decoded value.</summary>
    internal static byte[] ComputeHash(ReadOnlyMemory<byte> manifestUtf8, IReadOnlyList<EventOptionRule> schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (manifestUtf8.Length == 0 || manifestUtf8.Length > 64 * 1024 * 1024)
        {
            throw new ArgumentException("RegistryLimit: invalid options manifest length.", nameof(manifestUtf8));
        }

        // Reserve a conservative parse/validation/text/value footprint before
        // copying or DOM creation; a larger capability requires streaming.
        long accounted = checked(manifestUtf8.Length * 16L + schema.Count * 256L);
        if (accounted > 64L * 1024 * 1024)
        {
            throw new ArgumentException("RegistryLimit: options validation workspace exceeds 64 MiB.", nameof(manifestUtf8));
        }

        byte[] input = manifestUtf8.ToArray();
        byte[]? canonical = null;
        try
        {
            var scanner = new Utf8JsonReader(input, new JsonReaderOptions { MaxDepth = 64 });
            int nodes = 0;
            while (scanner.Read())
            {
                if (++nodes > 1_000_000 || checked(accounted + nodes * 128L) > 64L * 1024 * 1024)
                {
                    throw new ArgumentException("RegistryLimit: options node/validation workspace exceeds the admitted bound.");
                }
            }

            using JsonDocument supplied = JsonDocument.Parse(input, new JsonDocumentOptions { MaxDepth = 64 });
            if (supplied.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("An options manifest must be one canonical JSON object.", nameof(manifestUtf8));
            }

            canonical = EventCanonicalJsonValueCodec.EncodeText(supplied.RootElement, checked((int)((64L * 1024 * 1024 - accounted - nodes * 128L) / 2)));
            if (!canonical.AsSpan().SequenceEqual(input))
            {
                throw new ArgumentException("The supplied options text must be canonical UTF-8 with exactly one final LF.", nameof(manifestUtf8));
            }

            var rules = new Dictionary<string, EventOptionRule>(StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (EventOptionRule rule in schema)
            {
                ArgumentNullException.ThrowIfNull(rule);
                ArgumentException.ThrowIfNullOrEmpty(rule.Name);
                if (!names.Add(rule.Name) || !rules.TryAdd(rule.Name, rule)
                    || rule.Required && rule.CanonicalDefault is not null || !rule.Required && rule.CanonicalDefault is null)
                {
                    throw new ArgumentException("An option schema must have unique names and exact defaults only for optional fields.", nameof(schema));
                }
            }

            var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (JsonProperty property in supplied.RootElement.EnumerateObject())
            {
                if (!rules.TryGetValue(property.Name, out EventOptionRule? rule))
                {
                    throw new ArgumentException("Unknown implementation option.", nameof(manifestUtf8));
                }

                ValidateType(property.Value, rule);
                fields.Add(property.Name, property.Value);
            }

            var defaults = new List<JsonDocument>();
            var defaultTexts = new List<byte[]>();
            try
            {
                foreach (EventOptionRule rule in rules.Values)
                {
                    if (rule.Required)
                    {
                        if (!fields.ContainsKey(rule.Name)) { throw new ArgumentException("A required implementation option is missing.", nameof(manifestUtf8)); }
                        continue;
                    }
                    int defaultLength = new UTF8Encoding(false, true).GetByteCount(rule.CanonicalDefault!);
                    accounted = checked(accounted + defaultLength * 16L);
                    if (accounted > 64L * 1024 * 1024)
                    {
                        throw new ArgumentException("RegistryLimit: option defaults exceed the admitted workspace.", nameof(schema));
                    }

                    byte[] defaultText = new UTF8Encoding(false, true).GetBytes(rule.CanonicalDefault!);
                    byte[]? canonicalDefault = null;
                    bool retainedDefaultText = false;
                    try
                    {
                        JsonDocument document = JsonDocument.Parse(defaultText, new JsonDocumentOptions { MaxDepth = 64 });
                        defaults.Add(document);
                        defaultTexts.Add(defaultText);
                        retainedDefaultText = true;
                        ValidateType(document.RootElement, rule);
                        canonicalDefault = EventCanonicalJsonValueCodec.EncodeText(document.RootElement, checked((int)((64L * 1024 * 1024 - accounted) / 2)));
                        if (!canonicalDefault.AsSpan(0, canonicalDefault.Length - 1).SequenceEqual(defaultText))
                        {
                            throw new ArgumentException("An implementation's optional default must be exact canonical JSON.", nameof(schema));
                        }

                        if (!fields.ContainsKey(rule.Name)) { fields.Add(rule.Name, document.RootElement); }
                    }
                    finally
                    {
                        if (!retainedDefaultText) { CryptographicOperations.ZeroMemory(defaultText); }
                        if (canonicalDefault is not null) { CryptographicOperations.ZeroMemory(canonicalDefault); }
                    }
                }

                var encodedFields = new List<(byte[] Name, byte[] Value)>();
                try
                {
                    long total = 5;
                    foreach ((string name, JsonElement value) in fields)
                    {
                        int nameLength = new UTF8Encoding(false, true).GetByteCount(name);
                        int valueLength = EventCanonicalJsonValueCodec.MeasureValue(value);
                        long nextTotal = checked(total + 5 + nameLength + valueLength);
                        if (checked(accounted + nextTotal * 2) > 64L * 1024 * 1024)
                        {
                            throw new ArgumentException("RegistryLimit: expanded options exceed the admitted workspace.");
                        }

                        byte[] encodedName = new UTF8Encoding(false, true).GetBytes(name);
                        byte[] encodedValue = EventCanonicalJsonValueCodec.EncodeValue(value);
                        encodedFields.Add((encodedName, encodedValue));
                        total = checked(total + 5 + encodedName.Length + encodedValue.Length);
                        if (checked(accounted + total * 2) > 64L * 1024 * 1024)
                        {
                            throw new ArgumentException("RegistryLimit: expanded options exceed the admitted workspace.");
                        }
                    }

                    encodedFields.Sort(static (left, right) => left.Name.AsSpan().SequenceCompareTo(right.Name));
                    using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    hash.AppendData("HX-EV-OPTIONS-1\0"u8);
                    hash.AppendData([1]);
                    Span<byte> count = stackalloc byte[4];
                    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(count, checked((uint)total));
                    hash.AppendData(count);
                    hash.AppendData([6]);
                    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(count, checked((uint)encodedFields.Count));
                    hash.AppendData(count);
                    foreach ((byte[] name, byte[] value) in encodedFields)
                    {
                        hash.AppendData([1]);
                        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(count, checked((uint)name.Length));
                        hash.AppendData(count);
                        hash.AppendData(name);
                        hash.AppendData(value);
                    }

                    return hash.GetHashAndReset();
                }
                finally
                {
                    foreach ((byte[] name, byte[] value) in encodedFields)
                    {
                        CryptographicOperations.ZeroMemory(name);
                        CryptographicOperations.ZeroMemory(value);
                    }
                }
            }
            finally
            {
                foreach (JsonDocument document in defaults) { document.Dispose(); }
                foreach (byte[] defaultText in defaultTexts) { CryptographicOperations.ZeroMemory(defaultText); }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
            if (canonical is not null) { CryptographicOperations.ZeroMemory(canonical); }
        }
    }

    private static void ValidateType(JsonElement value, EventOptionRule rule)
    {
        bool boolean = rule.Kind is JsonValueKind.True or JsonValueKind.False;
        if ((boolean && value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            || !boolean && value.ValueKind != rule.Kind
            || rule.IntegerOnly && (value.ValueKind != JsonValueKind.Number
                || !EventCanonicalNumber.IsInteger(value.GetRawText())))
        {
            throw new ArgumentException("An implementation option has the wrong declared JSON type.", nameof(value));
        }
    }
}
