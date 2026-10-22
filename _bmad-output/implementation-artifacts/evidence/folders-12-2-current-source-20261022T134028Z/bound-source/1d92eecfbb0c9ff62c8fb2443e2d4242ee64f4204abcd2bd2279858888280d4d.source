using System.Security.Cryptography;
using System.Text.Json;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Computes target import fingerprints without retaining serialized payload bytes.</summary>
internal static class IdempotencyAdmissionPromotionEvidence
{
    /// <summary>Builds the bounded identity used only by ordinary digest-key promotion.</summary>
    public static string BuildConventionalMigrationId(string sourceActorId, string targetActorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceActorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetActorId);
        return string.Concat("promotion:", sourceActorId, ":", targetActorId);
    }

    /// <summary>Computes the exact imported target-state digest.</summary>
    public static string Compute(
        IdempotencyAdmissionRecord? record,
        IdempotencyAdmissionTombstone? tombstone)
    {
        if ((record is null) == (tombstone is null))
        {
            throw new InvalidOperationException("Exactly one promotion payload is required for evidence.");
        }

        byte[] bytes = record is not null
            ? JsonSerializer.SerializeToUtf8Bytes(record)
            : JsonSerializer.SerializeToUtf8Bytes(tombstone);
        try
        {
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
