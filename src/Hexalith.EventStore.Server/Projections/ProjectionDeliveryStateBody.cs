using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Checks guarded body budgets and unambiguous evidence before materializing any typed proof graph.</summary>
internal static class ProjectionDeliveryStateBody
{
    // Retain only the acyclic platform evidence ancestry. Unknown JSON is skipped
    // without property strings or one duplicate set per untrusted nesting level.
    private static readonly IReadOnlyDictionary<Type, PropertyInfo[]> EvidenceProperties = new[]
    {
        typeof(ProjectionDeliveryState), typeof(ProjectionDeliveryReservation), typeof(ProjectionDeliveryReceipt),
        typeof(ProjectionDeliveryGuardReservation), typeof(ProjectionDeliveryGuardEvidence),
        typeof(ProjectionDeliveryIdleBindingPending), typeof(SourceProof), typeof(ProjectionOwnerProof),
        typeof(ProjectionOwnerSelection),
    }.ToDictionary(static type => type, static type => type.GetProperties());
    private static readonly IReadOnlyDictionary<Type, (PropertyInfo Property, string Name, byte[] Utf8)[]> DefaultNames = CreateNames(null);
    private static readonly ConditionalWeakTable<JsonNamingPolicy, IReadOnlyDictionary<Type, (PropertyInfo Property, string Name, byte[] Utf8)[]>> PolicyNames = new();

    /// <summary>Leaves historical schema1 parsing unchanged while bounding every guarded proof before deserialization.</summary>
    internal static void Validate(ReadOnlySpan<byte> body, JsonSerializerOptions options, int receiptLimit)
    {
        if (body.IsEmpty) return;
        IReadOnlyDictionary<Type, (PropertyInfo Property, string Name, byte[] Utf8)[]> names = options.PropertyNamingPolicy is { } policy
            ? PolicyNames.GetValue(policy, static value => CreateNames(value)) : DefaultNames;
        if (!IsGuarded(body, options, names)) return;
        if (body.Length > ProjectionDeliveryStateSize.AbsoluteLimit) RejectSize();
        var reader = new Utf8JsonReader(body, ReaderOptions(options));
        Utf8JsonReader digests = default, receipts = default;
        long head = 0, completed = 0, retained = 0;
        bool longIdentity = false;
        try
        {
            if (!reader.Read()) return;
            InspectObject(ref reader, typeof(ProjectionDeliveryState), options, names, ref head, ref completed, ref retained,
                ref longIdentity, ref digests, ref receipts);
            while (reader.Read()) { }
        }
        finally { ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofParsingBytes, reader.BytesConsumed); }
        if (receipts.TokenType != JsonTokenType.None)
        {
            Utf8JsonReader receiptShape = receipts;
            ValidateReceipts(ref receiptShape, completed, retained, receiptLimit, options, names, ref longIdentity, false);
        }
        long sizeLimit = longIdentity || head < 0 || head > (long.MaxValue - 16_384L) / 592 ? ProjectionDeliveryStateSize.AbsoluteLimit
            : Math.Min(ProjectionDeliveryStateSize.AbsoluteLimit, 16_384L + 80 * head + 512 * Math.Min(head, receiptLimit));
        if (body.Length > sizeLimit) RejectSize();
        // Property order is arbitrary. Only after the complete header and body cap
        // are known may the digest list be validated, still before typed allocation.
        if (digests.TokenType != JsonTokenType.None) ValidateDigests(ref digests, completed);
        if (receipts.TokenType == JsonTokenType.None) RejectReceipts();
        ValidateReceipts(ref receipts, completed, retained, receiptLimit, options, names, ref longIdentity, true);
    }

    private static void InspectObject(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options,
        IReadOnlyDictionary<Type, (PropertyInfo Property, string Name, byte[] Utf8)[]> names,
        ref long head, ref long completed, ref long retained, ref bool longIdentity, ref Utf8JsonReader digests, ref Utf8JsonReader receipts)
    {
        if (reader.TokenType != JsonTokenType.StartObject) { reader.Skip(); return; }
        (PropertyInfo Property, string Name, byte[] Utf8)[] properties = names[type];
        ulong seen = 0;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("Invalid guarded evidence object.");
            int index = FindProperty(ref reader, properties, options.PropertyNameCaseInsensitive);
            if (!reader.Read()) throw new JsonException("Incomplete guarded evidence.");
            if (index < 0) { reader.Skip(); continue; }
            ulong bit = 1UL << index;
            if ((seen & bit) != 0) throw new JsonException("Duplicate guarded evidence property.");
            seen |= bit;
            PropertyInfo property = properties[index].Property;
            if (type == typeof(ProjectionDeliveryState) && property.Name == nameof(ProjectionDeliveryState.LastDeliveredSequence))
            {
                completed = JsonSerializer.Deserialize<long>(ref reader, options);
                head = Math.Max(head, completed);
            }
            else if (type == typeof(ProjectionDeliveryReservation) && property.Name == nameof(ProjectionDeliveryReservation.EndSequence))
                head = Math.Max(head, JsonSerializer.Deserialize<long>(ref reader, options));
            else if (type == typeof(ProjectionDeliveryState) && property.Name == nameof(ProjectionDeliveryState.FirstRetainedSequence))
                retained = JsonSerializer.Deserialize<long>(ref reader, options);
            else if (type == typeof(ProjectionDeliveryState) && property.Name == nameof(ProjectionDeliveryState.CompletedMessageIdDigests))
            {
                digests = reader;
                reader.Skip();
            }
            else if (type == typeof(ProjectionDeliveryState) && property.Name == nameof(ProjectionDeliveryState.CompletedReceipts))
            {
                receipts = reader;
                reader.Skip();
            }
            else if (reader.TokenType == JsonTokenType.String && IdentityLimit(type, property.Name) is { } limit)
            {
                string value = reader.GetString()!;
                longIdentity |= (limit < 0 ? value.Length : Encoding.UTF8.GetByteCount(value)) > Math.Abs(limit);
            }
            else if (EvidenceProperties.ContainsKey(property.PropertyType))
                InspectObject(ref reader, property.PropertyType, options, names, ref head, ref completed, ref retained,
                    ref longIdentity, ref digests, ref receipts);
            else reader.Skip();
        }
    }

    private static void ValidateReceipts(ref Utf8JsonReader reader, long completed, long retained, int receiptLimit,
        JsonSerializerOptions options, IReadOnlyDictionary<Type, (PropertyInfo Property, string Name, byte[] Utf8)[]> names,
        ref bool longIdentity, bool inspectText)
    {
        long start = reader.BytesConsumed, visited = 0;
        try
        {
            long count = ProjectionDeliveryReceiptValidation.GetExpectedCount(completed, retained, receiptLimit);
            if (reader.TokenType != JsonTokenType.StartArray) RejectReceipts();
            (PropertyInfo Property, string Name, byte[] Utf8)[] properties = names[typeof(ProjectionDeliveryReceipt)];
            Span<char> receiptIdentity = stackalloc char[216];
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (++visited > count || reader.TokenType != JsonTokenType.StartObject) RejectReceipts();
                ulong seen = 0;
                bool sequence = false, message = false, fingerprint = false, prefix = false;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) RejectReceipts();
                    int index = FindProperty(ref reader, properties, options.PropertyNameCaseInsensitive);
                    if (!reader.Read()) RejectReceipts();
                    if (index < 0) { reader.Skip(); continue; }
                    ulong bit = 1UL << index;
                    if ((seen & bit) != 0) throw new JsonException("Duplicate guarded evidence property.");
                    seen |= bit;
                    string name = properties[index].Property.Name;
                    if (name == nameof(ProjectionDeliveryReceipt.SequenceNumber))
                        sequence = JsonSerializer.Deserialize<long>(ref reader, options) == retained + visited - 1;
                    else
                    {
                        if (reader.TokenType != JsonTokenType.String) RejectReceipts();
                        if (reader.ValueSpan.IsEmpty || inspectText && string.IsNullOrWhiteSpace(reader.GetString())) RejectReceipts();
                        if (name == nameof(ProjectionDeliveryReceipt.MessageId))
                        {
                            message = true;
                            // Even an escaped UTF-16 character takes at most six UTF-8
                            // bytes. Determine the compatibility profile without allocating
                            // an untrusted string before its serialized body cap is known.
                            if (reader.ValueSpan.Length > 216) longIdentity = true;
                            else
                            {
                                longIdentity |= reader.CopyString(receiptIdentity) > 36;
                            }
                        }
                        else if (name == nameof(ProjectionDeliveryReceipt.EventFingerprint)) fingerprint = true;
                        else if (name == nameof(ProjectionDeliveryReceipt.PrefixFingerprint)) prefix = true;
                    }
                }
                if (!sequence || !message || !fingerprint || !prefix || reader.TokenType != JsonTokenType.EndObject) RejectReceipts();
            }
            if (reader.TokenType != JsonTokenType.EndArray || visited != count) RejectReceipts();
        }
        finally
        {
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofVisits, visited);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofParsingBytes, reader.BytesConsumed - start);
        }
    }

    private static void ValidateDigests(ref Utf8JsonReader reader, long completed)
    {
        if (reader.TokenType == JsonTokenType.Null) return; // Existing unproven rows remain classifiable.
        if (reader.TokenType != JsonTokenType.StartArray) RejectDigests();
        long count = 0;
        Span<byte> digest = stackalloc byte[32];
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (++count > completed || !ProjectionMessageIdDigestsJsonConverter.TryReadCanonicalDigest(ref reader, digest)) RejectDigests();
        }
        if (reader.TokenType != JsonTokenType.EndArray || count != completed) RejectDigests();
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofVisits, count);
    }

    private static int? IdentityLimit(Type type, string name)
    {
        if (type == typeof(ProjectionDeliveryState)) return name switch
        { "TenantId" or "Domain" or "AggregateId" or "ProjectionName" => 64, "LastCompletedMessageId" => -36, _ => null };
        if (type == typeof(ProjectionDeliveryReservation) && name == "HeadMessageId"
            || type == typeof(ProjectionDeliveryReceipt) && name == "MessageId") return -36;
        if (type == typeof(ProjectionDeliveryGuardReservation) && name == "SelectedOwnerGeneration") return 36;
        return type != typeof(ProjectionOwnerSelection) ? null : name switch
        { "OwnerIdentity" or "FreshnessVersion" => 64, "OwnerGeneration" => 36, "MessageId" or "BatchId" => -36, _ => null };
    }

    private static bool IsGuarded(ReadOnlySpan<byte> body, JsonSerializerOptions options,
        IReadOnlyDictionary<Type, (PropertyInfo Property, string Name, byte[] Utf8)[]> names)
    {
        var reader = new Utf8JsonReader(body, ReaderOptions(options));
        (PropertyInfo Property, string Name, byte[] Utf8)[] properties = names[typeof(ProjectionDeliveryState)];
        try
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return false;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("Invalid delivery object.");
                int index = FindProperty(ref reader, properties, options.PropertyNameCaseInsensitive);
                string? property = index >= 0 ? properties[index].Property.Name : null;
                if (!reader.Read()) throw new JsonException("Incomplete delivery object.");
                if (property == nameof(ProjectionDeliveryState.SchemaVersion)
                    && JsonSerializer.Deserialize<int>(ref reader, options) >= 2) return true;
                if (property is nameof(ProjectionDeliveryState.GuardEvidenceVersion) or nameof(ProjectionDeliveryState.GuardEvidence)
                    or nameof(ProjectionDeliveryState.IdleBindingPending) && reader.TokenType != JsonTokenType.Null) return true;
                reader.Skip();
            }
            return false;
        }
        finally { ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofParsingBytes, reader.BytesConsumed); }
    }

    private static IReadOnlyDictionary<Type, (PropertyInfo Property, string Name, byte[] Utf8)[]> CreateNames(JsonNamingPolicy? policy)
        => EvidenceProperties.ToDictionary(static pair => pair.Key, pair => pair.Value.Select(property =>
        {
            string name = policy?.ConvertName(property.Name) ?? property.Name;
            return (property, name, Encoding.UTF8.GetBytes(name));
        }).ToArray());

    private static int FindProperty(ref Utf8JsonReader reader,
        (PropertyInfo Property, string Name, byte[] Utf8)[] properties, bool ignoreCase)
    {
        int maximum = 0;
        for (int index = 0; index < properties.Length; index++) maximum = Math.Max(maximum, properties[index].Utf8.Length);
        // Reject impossible lengths even before escaped-name comparison: no
        // untrusted oversized name needs to be decoded to establish this bound.
        if (reader.ValueSpan.Length > 6L * maximum) return -1;
        for (int index = 0; index < properties.Length; index++)
            if (reader.ValueTextEquals(properties[index].Utf8)) return index;
        if (!ignoreCase) return -1;
        char[]? rented = null;
        Span<char> actual = reader.ValueSpan.Length <= 512 ? stackalloc char[512]
            : rented = ArrayPool<char>.Shared.Rent(reader.ValueSpan.Length);
        try
        {
            int length = reader.CopyString(actual);
            for (int index = 0; index < properties.Length; index++)
                if (actual[..length].Equals(properties[index].Name.AsSpan(), StringComparison.OrdinalIgnoreCase)) return index;
            return -1;
        }
        finally { if (rented is not null) ArrayPool<char>.Shared.Return(rented); }
    }

    private static JsonReaderOptions ReaderOptions(JsonSerializerOptions options) => new()
    { AllowTrailingCommas = options.AllowTrailingCommas, CommentHandling = options.ReadCommentHandling, MaxDepth = options.MaxDepth };

    private static void RejectDigests() => throw new ProjectionDeliveryHistoryValidationException("delivery_reconciliation_required");
    private static void RejectReceipts() => throw new ProjectionDeliveryHistoryValidationException("delivery_reconciliation_required");
    private static void RejectSize() => throw new ProjectionDeliveryHistoryValidationException("delivery_proof_limit_exceeded");
}
