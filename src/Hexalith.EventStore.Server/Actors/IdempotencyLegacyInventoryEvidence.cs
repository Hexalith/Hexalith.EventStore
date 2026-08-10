using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Creates protected closure digests without retaining source payload bytes.</summary>
internal static class IdempotencyLegacyInventoryEvidence
{
    /// <summary>Computes the protected fingerprint for one inventory entry.</summary>
    public static string ComputeEntryDigest(IdempotencyLegacyInventoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var evidence = new ImmutableEntryEvidence(
            entry.InventoryId,
            entry.InventoryVersion,
            entry.MigrationId,
            entry.SchemaVersion,
            entry.TenantPartition,
            entry.SourceAggregateActorId,
            entry.SourceEvidenceDigest,
            entry.LegacySchemaVersion,
            entry.DigestKeyVersion,
            entry.KeyDigest,
            entry.VerificationTag,
            entry.IntentDigest,
            entry.RetentionTier,
            entry.FirstConsumedAt,
            entry.LastObservedAt,
            entry.ReplayExpiresAt,
            entry.ReplayResult,
            entry.ExecutionMessageId,
            entry.ExecutionCorrelationId);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(evidence);
        try
        {
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>Computes the canonical digest of an ordered closed manifest.</summary>
    public static string ComputeManifestDigest(
        int schemaVersion,
        string tenantPartition,
        string inventoryId,
        int inventoryVersion,
        IEnumerable<string> entryDigests,
        IEnumerable<string> digestKeyVersions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantPartition);
        ArgumentException.ThrowIfNullOrWhiteSpace(inventoryId);
        ArgumentNullException.ThrowIfNull(entryDigests);
        ArgumentNullException.ThrowIfNull(digestKeyVersions);
        string[] sortedEntryDigests = entryDigests.Order(StringComparer.Ordinal).ToArray();
        var evidence = new ManifestEvidence(
            schemaVersion,
            tenantPartition,
            inventoryId,
            inventoryVersion,
            sortedEntryDigests.Length,
            digestKeyVersions.Order(StringComparer.Ordinal).ToArray(),
            sortedEntryDigests);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(evidence);
        try
        {
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private sealed record ImmutableEntryEvidence(
        string InventoryId,
        int InventoryVersion,
        string MigrationId,
        int SchemaVersion,
        string TenantPartition,
        string SourceAggregateActorId,
        string SourceEvidenceDigest,
        int LegacySchemaVersion,
        string DigestKeyVersion,
        string KeyDigest,
        string VerificationTag,
        string IntentDigest,
        IdempotencyReplayRetentionTier RetentionTier,
        DateTimeOffset FirstConsumedAt,
        DateTimeOffset LastObservedAt,
        DateTimeOffset ReplayExpiresAt,
        CommandProcessingResult ReplayResult,
        string ExecutionMessageId,
        string ExecutionCorrelationId);

    private sealed record ManifestEvidence(
        int SchemaVersion,
        string TenantPartition,
        string InventoryId,
        int InventoryVersion,
        int EntryCount,
        string[] DigestKeyVersions,
        string[] EntryDigests);
}
