using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Checks guarded body budgets and unambiguous evidence before materializing any typed proof graph.</summary>
internal static class ProjectionDeliveryStateBody
{
    /// <summary>Leaves historical schema1 parsing unchanged while bounding every guarded proof before deserialization.</summary>
    internal static void Validate(ReadOnlySpan<byte> body, JsonSerializerOptions options, int receiptLimit)
    {
        if (body.IsEmpty || !IsGuarded(body, options)) return;
        if (body.Length > ProjectionDeliveryStateSize.AbsoluteLimit) RejectSize();
        var reader = new Utf8JsonReader(body, ReaderOptions(options));
        string? name = null;
        // Known evidence paths inspect containers only through depth four. Deeper
        // unknown values retain the configured reader limit without reserving that limit.
        var containers = new string?[5];
        long head = 0;
        bool longIdentity = false;
        try
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName) name = reader.GetString();
                else if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                {
                    if (reader.CurrentDepth < containers.Length) containers[reader.CurrentDepth] = name;
                    name = null;
                }
                else if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray) name = null;
                else if (name is not null)
                {
                    int depth = reader.CurrentDepth;
                    bool Root(string property) => depth == 1 && Matches(name, property, options);
                    bool Reservation(string property) => depth == 2
                        && Matches(containers[1], "ActiveReservation", options) && Matches(name, property, options);
                    if (reader.TokenType == JsonTokenType.Number && (Root("LastDeliveredSequence") || Reservation("EndSequence")))
                    {
                        if (!reader.TryGetInt64(out long value)) throw new JsonException("Invalid guarded head.");
                        head = Math.Max(head, value);
                    }
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        int? limit = Root("TenantId") || Root("Domain") || Root("AggregateId") || Root("ProjectionName") ? 64
                            : Root("LastCompletedMessageId") || Reservation("HeadMessageId") ? -36 : null;
                        bool owner = reader.CurrentDepth == 4 && Matches(containers[2], "OwnerProof", options)
                                && (Matches(containers[1], "GuardEvidence", options) || Matches(containers[1], "IdleBindingPending", options))
                            || reader.CurrentDepth == 5 && Matches(containers[1], "ActiveReservation", options)
                                && Matches(containers[2], "GuardBinding", options) && Matches(containers[3], "BaseOwnerProof", options);
                        if (owner && Matches(containers[reader.CurrentDepth - 1], "Selection", options))
                        {
                            if (Matches(name, "OwnerIdentity", options) || Matches(name, "FreshnessVersion", options)) limit = 64;
                            if (Matches(name, "OwnerGeneration", options)) limit = 36;
                            if (Matches(name, "MessageId", options) || Matches(name, "BatchId", options)) limit = -36;
                        }
                        if (reader.CurrentDepth == 3 && Matches(containers[1], "ActiveReservation", options)
                            && Matches(containers[2], "GuardBinding", options) && Matches(name, "SelectedOwnerGeneration", options)) limit = 36;
                        if (reader.CurrentDepth == 3 && Matches(containers[1], "CompletedReceipts", options)
                            && Matches(name, "MessageId", options)) limit = -36;
                        if (limit is { } maximum)
                        {
                            string value = reader.GetString()!;
                            // Existing receipt/message compatibility uses character lengths; scope/owner references use UTF-8.
                            longIdentity |= (maximum < 0 ? value.Length : Encoding.UTF8.GetByteCount(value)) > Math.Abs(maximum);
                        }
                    }
                    name = null;
                }
            }
        }
        finally { ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofParsingBytes, reader.BytesConsumed); }
        long sizeLimit = longIdentity || head > (long.MaxValue - 16_384L) / 592 ? ProjectionDeliveryStateSize.AbsoluteLimit
            : Math.Min(ProjectionDeliveryStateSize.AbsoluteLimit, 16_384L + 80 * head + 512 * Math.Min(head, receiptLimit));
        if (body.Length > sizeLimit) RejectSize();
        RejectDuplicates(body, options);
    }

    private static bool IsGuarded(ReadOnlySpan<byte> body, JsonSerializerOptions options)
    {
        var reader = new Utf8JsonReader(body, ReaderOptions(options));
        bool schema = false, evidence = false;
        try
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == 1)
                {
                    string? property = reader.GetString();
                    schema = Matches(property, "SchemaVersion", options);
                    evidence = Matches(property, "GuardEvidenceVersion", options) || Matches(property, "GuardEvidence", options)
                        || Matches(property, "IdleBindingPending", options);
                }
                else
                {
                    if (schema && reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int version) && version >= 2
                        || evidence && reader.TokenType != JsonTokenType.Null) return true;
                    schema = evidence = false;
                }
            }
            return false;
        }
        finally { ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofParsingBytes, reader.BytesConsumed); }
    }

    private static void RejectDuplicates(ReadOnlySpan<byte> body, JsonSerializerOptions options)
    {
        var reader = new Utf8JsonReader(body, ReaderOptions(options));
        var objects = new Stack<HashSet<string>>();
        StringComparer comparer = options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        try
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.StartObject) objects.Push(new(comparer));
                else if (reader.TokenType == JsonTokenType.EndObject) objects.Pop();
                else if (reader.TokenType == JsonTokenType.PropertyName && !objects.Peek().Add(reader.GetString()!))
                    throw new JsonException("Duplicate guarded evidence property.");
            }
        }
        finally { ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofParsingBytes, reader.BytesConsumed); }
    }

    private static bool Matches(string? actual, string property, JsonSerializerOptions options)
        => string.Equals(actual, options.PropertyNamingPolicy?.ConvertName(property) ?? property,
            options.PropertyNameCaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static JsonReaderOptions ReaderOptions(JsonSerializerOptions options) => new()
    { AllowTrailingCommas = options.AllowTrailingCommas, CommentHandling = options.ReadCommentHandling, MaxDepth = options.MaxDepth };

    private static void RejectSize() => throw new ProjectionDeliveryHistoryValidationException("delivery_proof_limit_exceeded");
}
