using System.Text.Json;
using System.Text.RegularExpressions;

using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Testing;

/// <summary>Checks actual provider observations for a fresh disposable store before the supported authenticated writer cutover.</summary>
/// <remarks>The caller must supply a complete atomic provider inventory. These checks never activate a writer or supply runtime authority.</remarks>
public static class ProjectionDeliveryCutoverObserver
{
    /// <summary>Gets the actual SDK store-global writer protocol key.</summary>
    public static string WriterProtocolKey => ProjectionDeliveryStateKeys.WriterProtocol;

    /// <summary>Identifies SDK delivery, activation, checkpoint, reconciliation, and lifecycle keys that must be inventoried before fresh cutover.</summary>
    public static bool IsRelevantStateKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key.StartsWith("projection-activation:", StringComparison.Ordinal)
            || key.StartsWith("projection-checkpoints", StringComparison.Ordinal)
            || key.StartsWith("projection-delivery-", StringComparison.Ordinal)
            || key.StartsWith("projection-rebuild", StringComparison.Ordinal)
            || key.StartsWith("eventstore||ProjectionLifecycleActor||", StringComparison.Ordinal);
    }

    /// <summary>Requires explicit empty current ledgers and no other SDK delivery state; missing or malformed ledger fields never establish quiescence.</summary>
    public static bool IsFreshStoreQuiescent(IReadOnlyDictionary<string, ReadOnlyMemory<byte>> observed, JsonSerializerOptions serializer)
    {
        ArgumentNullException.ThrowIfNull(observed);
        ArgumentNullException.ThrowIfNull(serializer);
        if (observed.Count > 256) return false;
        try
        {
            string Name(string value) => serializer.PropertyNamingPolicy?.ConvertName(value) ?? value;
            foreach (var (key, bytes) in observed)
            {
                if (!IsRelevantStateKey(key) || bytes.Length is 0 or > 1024 * 1024) return false;
                if (key == "projection-delivery-retry:protocol")
                {
                    if (JsonSerializer.Deserialize<string>(bytes.Span, serializer) != "v2-ready:v1-writers-quiesced") return false;
                    continue;
                }
                using JsonDocument document = JsonDocument.Parse(bytes);
                if (Regex.IsMatch(key, "^projection-activation:outbox:v1:[0-3][0-9a-f]$", RegexOptions.CultureInvariant))
                {
                    if (!HasExactProperties(document.RootElement, Name("Items"), Name("LastRevision"))) return false;
                    ProjectionActivationLedger? ledger = document.RootElement.Deserialize<ProjectionActivationLedger>(serializer);
                    if (ledger?.Items is not { Count: 0 } || ledger.LastRevision < 0) return false;
                }
                else if (Regex.IsMatch(key, "^projection-delivery-retry:ledger:v2:[0-3][0-9a-f]$", RegexOptions.CultureInvariant))
                {
                    if (!HasExactProperties(document.RootElement, Name("Items"))) return false;
                    ProjectionDeliveryRetryLedger? ledger = document.RootElement.Deserialize<ProjectionDeliveryRetryLedger>(serializer);
                    if (ledger?.Items is not { Count: 0 }) return false;
                }
                else return false;
            }
            return true;
        }
        catch (Exception failure) when (failure is JsonException or ArgumentException or InvalidOperationException or OverflowException) { return false; }
    }

    /// <summary>Allows only one explicitly source-bound completed bootstrap scope in an otherwise fresh quiescent store.</summary>
    /// <remarks>A historical non-zero row is accepted only before reconciliation; the caller must repeat with allowLegacy false before activating.</remarks>
    public static bool IsQuiescentWithCompletedSource(IReadOnlyDictionary<string, ReadOnlyMemory<byte>> observed,
        StreamReadPage source, string projection, string operatorId, bool allowLegacy, JsonSerializerOptions serializer)
    {
        ArgumentNullException.ThrowIfNull(observed);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(serializer);
        try
        {
            if (string.IsNullOrWhiteSpace(source.AggregateId)) return false;
            var identity = new AggregateIdentity(source.Tenant, source.Domain, source.AggregateId);
            string checkpoint = ProjectionDeliveryStateKeys.GetStateKey(identity, projection);
            string migration = ProjectionCheckpointTracker.GetMigratedMarkerKey(identity, projection);
            string work = ProjectionDeliveryStateKeys.GetReconciliationKey(identity, projection);
            string actor = "eventstore||ProjectionLifecycleActor||" + identity.ActorId + ":" + projection;
            string epochKey = actor + "||projection-lifecycle-epoch";
            if (string.IsNullOrWhiteSpace(operatorId) || observed.ContainsKey(actor + "||projection-lifecycle")
                || !observed.TryGetValue(checkpoint, out var body) || !observed.TryGetValue(epochKey, out var epoch)
                || epoch.Length is 0 or > 32 || JsonSerializer.Deserialize<long>(epoch.Span, serializer) <= 0
                || !(allowLegacy && IsLegacySourceCheckpoint(body, source, serializer))
                    && !ProjectionDeliveryCompletionObserver.IsComplete(source, projection, body, epoch, true, epoch, serializer)) return false;
            var remaining = observed.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            remaining.Remove(checkpoint); remaining.Remove(epochKey);
            string Name(string value) => serializer.PropertyNamingPolicy?.ConvertName(value) ?? value;
            if (remaining.Remove(migration, out var marker))
            {
                using var json = JsonDocument.Parse(marker);
                if (!HasExactProperties(json.RootElement, Name("Migrated"))
                    || json.RootElement.GetProperty(Name("Migrated")).ValueKind != JsonValueKind.True) return false;
            }
            if (remaining.Remove(work, out var reconciliation))
            {
                if (allowLegacy) return false;
                using var workJson = JsonDocument.Parse(reconciliation);
                if (!HasExactProperties(workJson.RootElement, Name("TenantId"), Name("Domain"), Name("AggregateId"), Name("ProjectionName"),
                    Name("ReasonCode"), Name("ObservedSequence"), Name("DeliveryStateVersion"), Name("OperatorId"), Name("RecordedAt"))) return false;
                ProjectionDeliveryReconciliationWork? receipt = workJson.RootElement.Deserialize<ProjectionDeliveryReconciliationWork>(serializer);
                if (receipt is null || receipt.TenantId != source.Tenant || receipt.Domain != source.Domain
                    || receipt.AggregateId != source.AggregateId || receipt.ProjectionName != projection
                    || receipt.ReasonCode != "delivery_reconciled" || receipt.OperatorId != operatorId
                    || receipt.ObservedSequence != source.Metadata.LatestSequence || receipt.DeliveryStateVersion != 1
                    || receipt.RecordedAt == DateTimeOffset.MinValue || receipt.RecordedAt > DateTimeOffset.UtcNow
                    || receipt.SourceGuardMaintenance is not null || receipt.QualifiedSourceGuard is not null) return false;
            }
            return IsFreshStoreQuiescent(remaining, serializer);
        }
        catch (Exception failure) when (failure is JsonException or ArgumentException or InvalidOperationException or OverflowException) { return false; }
    }

    /// <summary>Requires the actual historical five-field checkpoint to equal the exact readable bootstrap source head.</summary>
    public static bool IsLegacySourceCheckpoint(ReadOnlyMemory<byte> bytes, StreamReadPage source, JsonSerializerOptions serializer)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(serializer);
        if (bytes.Length is 0 or > 4096 || source.Events.Count == 0 || source.Metadata.LatestSequence != source.Events.Count) return false;
        try
        {
            if (StreamReadPageValidator.ValidateAndGetNextSequence(new(source.Tenant, source.Domain, source.AggregateId, PageSize: 256), source)
                != source.Metadata.LatestSequence) return false;
            using var document = JsonDocument.Parse(bytes);
            string Name(string value) => serializer.PropertyNamingPolicy?.ConvertName(value) ?? value;
            if (!HasExactProperties(document.RootElement, Name("TenantId"), Name("Domain"), Name("AggregateId"),
                Name("LastDeliveredSequence"), Name("UpdatedAt"))) return false;
            ProjectionCheckpoint? row = document.RootElement.Deserialize<ProjectionCheckpoint>(serializer);
            return row is not null && row.TenantId == source.Tenant && row.Domain == source.Domain
                && row.AggregateId == source.AggregateId && row.LastDeliveredSequence == source.Metadata.LatestSequence;
        }
        catch (Exception failure) when (failure is JsonException or ArgumentException or InvalidOperationException or OverflowException) { return false; }
    }

    /// <summary>Binds the real persisted marker to the requested SDK source commit and the actual activation time window.</summary>
    public static bool WriterProtocolMatches(ReadOnlyMemory<byte> bytes, string commit, DateTimeOffset notBefore,
        DateTimeOffset notAfter, JsonSerializerOptions serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        if (!Regex.IsMatch(commit, "^[0-9a-f]{40}$", RegexOptions.CultureInvariant) || bytes.Length is 0 or > 4096 || notBefore > notAfter) return false;
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes);
            string Name(string value) => serializer.PropertyNamingPolicy?.ConvertName(value) ?? value;
            if (!HasExactProperties(document.RootElement, Name("SchemaVersion"), Name("WriterProtocolVersion"),
                Name("CutoverCommit"), Name("ActivatedAt"), Name("IsCurrent"))) return false;
            ProjectionDeliveryWriterProtocol? marker = document.RootElement.Deserialize<ProjectionDeliveryWriterProtocol>(serializer);
            return marker is { IsCurrent: true } && marker.CutoverCommit == commit
                && document.RootElement.GetProperty(Name("IsCurrent")).ValueKind == JsonValueKind.True
                && marker.ActivatedAt >= notBefore && marker.ActivatedAt <= notAfter;
        }
        catch (Exception failure) when (failure is JsonException or ArgumentException or InvalidOperationException or OverflowException) { return false; }
    }

    private static bool HasExactProperties(JsonElement value, params string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object) return false;
        var expected = new HashSet<string>(names, StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject()) if (!expected.Remove(property.Name)) return false;
        return expected.Count == 0;
    }
}
