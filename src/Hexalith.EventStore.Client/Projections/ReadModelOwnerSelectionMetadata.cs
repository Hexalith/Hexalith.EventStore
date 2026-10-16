using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Validates optional selection evidence contained in the immutable model manifest.</summary>
internal static class ReadModelOwnerSelectionMetadata
{
    /// <summary>The reserved top-level persisted model metadata property.</summary>
    internal const string PropertyName = "projectionOwner";

    /// <summary>Extracts an explicitly authored owner selection from one model value.</summary>
    internal static ProjectionOwnerSelection? Read(ReadOnlyMemory<byte> bytes)
    {
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;
        JsonProperty[] owners = [.. root.EnumerateObject().Where(static property =>
            property.Name.Equals(PropertyName, StringComparison.OrdinalIgnoreCase))];
        if (owners.Length == 0) return null;
        JsonProperty[] versions = [.. root.EnumerateObject().Where(static property =>
            property.Name.Equals("projectionVersion", StringComparison.OrdinalIgnoreCase))];
        JsonElement metadata = owners[0].Value;
        if (owners.Length != 1 || versions.Length != 1
            || metadata.ValueKind != JsonValueKind.Object
            || metadata.EnumerateObject().Select(static property => property.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != metadata.EnumerateObject().Count())
            throw new ArgumentException("Invalid selected-owner metadata.");
        ProjectionOwnerSelection selection = metadata.Deserialize<ProjectionOwnerSelection>(JsonSerializerOptions.Web)
            ?? throw new ArgumentException("Invalid selected-owner metadata.");
        if (selection.Version != 1 || selection.AppliedSequence <= 0
            || new[] { selection.TenantId, selection.Domain, selection.AggregateId, selection.ProjectionName,
                selection.OwnerIdentity, selection.OwnerGeneration, selection.MessageId, selection.BatchId,
                selection.FreshnessVersion }.Any(string.IsNullOrWhiteSpace)
            || versions[0].Value.ValueKind != JsonValueKind.String || versions[0].Value.GetString() != selection.FreshnessVersion)
            throw new ArgumentException("Invalid selected-owner freshness binding.");
        return selection;
    }

    /// <summary>Derives receipt evidence from the same operations bound by the existing manifest fingerprint.</summary>
    internal static ProjectionOwnerSelection? FromBatch(ReadModelBatch batch)
    {
        if (batch.OwnerSelection is null) return null;
        if (batch.OwnerSelections is not null) throw new ArgumentException("A batch cannot declare two owner evidence forms.");
        ProjectionOwnerSelection? selection = null;
        foreach (ReadModelBatchOperation operation in batch.Operations)
        {
            if (operation.Kind == ReadModelBatchOperationKind.Delete) continue;
            ProjectionOwnerSelection? candidate = Read(operation.CanonicalValue);
            if (candidate is null) continue;
            if (candidate.TenantId != batch.Scope.TenantId || candidate.Domain != batch.Scope.Domain
                || candidate.AggregateId != batch.Scope.AggregateId || candidate.ProjectionName != batch.Scope.ProjectionType
                || candidate.BatchId != batch.Scope.BatchId || candidate.ReceiptProjectionName is not null
                || selection is not null && selection != candidate)
                throw new ArgumentException("Selected owner does not match the immutable batch scope.");
            selection = candidate;
        }

        if (selection != batch.OwnerSelection) throw new ArgumentException("The batch selection is absent from its immutable model operations.");
        return selection;
    }

    /// <summary>Derives coordinated selections only from explicitly enrolled, fingerprint-bound model operations.</summary>
    internal static IReadOnlyList<ProjectionOwnerSelection>? FromCoordinatedBatch(ReadModelBatch batch)
    {
        if (batch.OwnerSelections is null) return null;
        if (batch.OwnerSelection is not null || batch.Scope.ProjectionType != "rebuild" || batch.OwnerSelections.Count == 0)
            throw new ArgumentException("Invalid coordinated owner evidence.");
        var selected = new Dictionary<string, ProjectionOwnerSelection>(StringComparer.Ordinal);
        foreach (ReadModelBatchOperation operation in batch.Operations)
        {
            if (operation.Kind == ReadModelBatchOperationKind.Delete) continue;
            ProjectionOwnerSelection? candidate = Read(operation.CanonicalValue);
            if (candidate is null) continue;
            if (candidate.TenantId != batch.Scope.TenantId || candidate.Domain != batch.Scope.Domain
                || candidate.AggregateId != batch.Scope.AggregateId || candidate.BatchId != batch.Scope.BatchId
                || candidate.ReceiptProjectionName != batch.Scope.ProjectionType
                || selected.TryGetValue(candidate.ProjectionName, out ProjectionOwnerSelection? prior) && prior != candidate)
                throw new ArgumentException("Selected owner does not match the immutable coordinated batch scope.");
            selected[candidate.ProjectionName] = candidate;
        }

        ProjectionOwnerSelection[] result = [.. selected.Values.OrderBy(static item => item.ProjectionName, StringComparer.Ordinal)];
        if (!result.SequenceEqual(batch.OwnerSelections))
            throw new ArgumentException("The coordinated selections are absent from their immutable model operations.");
        return result;
    }

    /// <summary>Finds one exact selection in either existing receipt representation, rejecting ambiguous bindings.</summary>
    internal static bool ReceiptContains(ReadModelBatchMarker receipt, ProjectionOwnerSelection selected)
        => selected.ReceiptProjectionName is null
            ? receipt.OwnerSelection == selected && receipt.OwnerSelections is null
            : selected.ReceiptProjectionName == "rebuild" && receipt.OwnerSelection is null
                && receipt.OwnerSelections is { Count: > 0 } owners
                && owners.Select(static item => item.ProjectionName).Distinct(StringComparer.Ordinal).Count() == owners.Count
                && owners.All(item => item.TenantId == selected.TenantId && item.Domain == selected.Domain
                    && item.AggregateId == selected.AggregateId && item.BatchId == selected.BatchId
                    && item.ReceiptProjectionName == selected.ReceiptProjectionName)
                && owners.Count(item => item == selected) == 1;
}
