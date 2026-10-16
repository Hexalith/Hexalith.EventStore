using System.Text.Json;
using System.Text.Json.Serialization;

using ByteAether.Ulid;

using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Immutable maintenance-qualified admission of one exact source and projection scope.</summary>
internal sealed record ProjectionSourceGuard(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("guardVersion")] int GuardVersion,
    [property: JsonPropertyName("writerProtocolVersion")] int WriterProtocolVersion,
    [property: JsonPropertyName("deliverySchemaVersion")] int DeliverySchemaVersion,
    [property: JsonPropertyName("sourceCodecVersion")] int SourceCodecVersion,
    [property: JsonPropertyName("tenantId")] string TenantId,
    [property: JsonPropertyName("domain")] string Domain,
    [property: JsonPropertyName("aggregateId")] string AggregateId,
    [property: JsonPropertyName("projectionName")] string ProjectionName,
    [property: JsonPropertyName("sourceIncarnation")] string SourceIncarnation,
    [property: JsonPropertyName("catalogFingerprint")] string CatalogFingerprint,
    [property: JsonPropertyName("handlerSemantics")] string HandlerSemantics,
    [property: JsonPropertyName("specSha256")] string SpecSha256,
    [property: JsonPropertyName("cutoverCommit")] string CutoverCommit,
    [property: JsonPropertyName("activatedAt")] DateTimeOffset ActivatedAt)
{
    /// <summary>The exact owner-approved normative digest.</summary>
    internal const string ApprovedSpecSha256 = "78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711";

    private static readonly HashSet<string> PropertyNames = new(StringComparer.Ordinal)
    {
        "schemaVersion", "guardVersion", "writerProtocolVersion", "deliverySchemaVersion", "sourceCodecVersion",
        "tenantId", "domain", "aggregateId", "projectionName", "sourceIncarnation", "catalogFingerprint",
        "handlerSemantics", "specSha256", "cutoverCommit", "activatedAt",
    };

    /// <summary>Validates exact versions, identity, inventory binding and approval without inferring defaults.</summary>
    internal bool IsValid(AggregateIdentity identity, string projectionName)
        => SchemaVersion == 1 && GuardVersion == 1 && WriterProtocolVersion == 2
            && DeliverySchemaVersion == 2 && SourceCodecVersion == 1
            && string.Equals(TenantId, identity.TenantId, StringComparison.Ordinal)
            && string.Equals(Domain, identity.Domain, StringComparison.Ordinal)
            && string.Equals(AggregateId, identity.AggregateId, StringComparison.Ordinal)
            && string.Equals(ProjectionName, projectionName, StringComparison.Ordinal)
            && Ulid.TryParse(SourceIncarnation, null, out _)
            && IsLowerHex(CatalogFingerprint, 64) && IsLowerHex(CutoverCommit, 40)
            && HandlerSemantics is "single-event-v1" or "mapped-incremental-replay-v1" or "shared-epoch-v1" or "full-replay-v1"
            && string.Equals(SpecSha256, ApprovedSpecSha256, StringComparison.Ordinal)
            && ActivatedAt != default && ActivatedAt.Offset == TimeSpan.Zero;

    /// <summary>Parses only the exact bounded JSON shape, rejecting duplicate and unknown properties.</summary>
    internal static ProjectionSourceGuard? Parse(JsonElement element, JsonSerializerOptions options)
    {
        if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return null;
        if (element.ValueKind != JsonValueKind.Object
            || JsonSerializer.SerializeToUtf8Bytes(element, options).Length > 4096)
            throw new JsonException("Invalid source guard record.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!PropertyNames.Contains(property.Name) || !seen.Add(property.Name))
                throw new JsonException("Invalid source guard property.");
        }

        if (seen.Count != PropertyNames.Count) throw new JsonException("Incomplete source guard record.");
        return element.Deserialize<ProjectionSourceGuard>(options)
            ?? throw new JsonException("Invalid source guard record.");
    }

    private static bool IsLowerHex(string? value, int length)
        => value is not null && value.Length == length
            && value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
