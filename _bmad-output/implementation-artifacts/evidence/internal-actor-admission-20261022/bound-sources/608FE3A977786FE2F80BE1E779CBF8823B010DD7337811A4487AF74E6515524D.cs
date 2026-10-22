using System.Security.Cryptography;
using System.Text.Json;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Computes exact protected source and payload-free redirect fingerprints.</summary>
internal static class IdempotencyLegacySourceEvidence
{
    /// <summary>Computes the exact supported source-record digest.</summary>
    public static string Compute(IdempotencyRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(record);
        try
        {
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>Computes the payload-free source redirect digest.</summary>
    public static string Compute(IdempotencyLegacySourceRedirectRecord redirect)
    {
        ArgumentNullException.ThrowIfNull(redirect);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(redirect);
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
