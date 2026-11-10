// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base section 10.2; owner decision D1 (2026-11-10) implementation rule, no amendment.
using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Places index entries by linear hashing over the head's page count without changing any record shape, key or order.</summary>
/// <remarks>
/// The hash is the big-endian unsigned 64-bit prefix of SHA-256 over the codec entry identity. With L = floor(log2 P) and
/// s = P - 2^L, an entry belongs to bucket h mod 2^L, or to h mod 2^(L+1) when that first bucket is below s. Page 0 is created
/// when P = 0; otherwise adding entry n + 1 with n + 1 > 64 P splits bucket s into the new page P, which remaps no other bucket.
/// </remarks>
internal static class PayloadIndexPlacement
{
    /// <summary>Gets the mean entries per page above which one insertion splits one bucket.</summary>
    internal const int AverageLoad = 64;

    /// <summary>Gets the approved per-page entry cap enforced by the state codec.</summary>
    internal const int PageCapacity = 256;

    /// <summary>Computes the big-endian unsigned 64-bit prefix of SHA-256 over an exact codec entry identity.</summary>
    internal static ulong Hash(string identity)
    {
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        _ = SHA256.HashData(Encoding.UTF8.GetBytes(identity), digest);
        return BinaryPrimitives.ReadUInt64BigEndian(digest);
    }

    /// <summary>Maps a hash to its bucket page for a positive page count.</summary>
    internal static uint Bucket(ulong hash, uint pageCount)
    {
        if (pageCount == 0) { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ConsistencyMismatch); }
        int level = BitOperations.Log2(pageCount);
        ulong bucket = hash & ((1UL << level) - 1);
        return checked((uint)(bucket < SplitPage(pageCount) ? hash & ((2UL << level) - 1) : bucket));
    }

    /// <summary>Maps an exact codec entry identity to its bucket page for a positive page count.</summary>
    internal static uint Bucket(string identity, uint pageCount) => Bucket(Hash(identity), pageCount);

    /// <summary>Returns whether adding one entry to n entries on P pages first creates page 0 or splits bucket s into page P.</summary>
    internal static bool Grows(uint pageCount, ulong entryCount) => pageCount == 0 || checked(entryCount + 1) > checked((ulong)AverageLoad * pageCount);

    /// <summary>Returns s = P - 2^floor(log2 P), the next bucket to split, for a positive page count.</summary>
    internal static uint SplitPage(uint pageCount)
        => pageCount == 0 ? throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ConsistencyMismatch)
            : pageCount - (1u << BitOperations.Log2(pageCount));
}
