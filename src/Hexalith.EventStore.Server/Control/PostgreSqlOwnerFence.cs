using System.Security.Cryptography;

namespace Hexalith.EventStore.Server.Control;

/// <summary>Mints a canonical time-ordered ULID fence with 80 cryptographically random bits.</summary>
/// <remarks>The transaction owner must still authenticate transfer authority and forbid reuse across retained incarnations.</remarks>
internal static class PostgreSqlOwnerFence
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const long MaximumTimestamp = (1L << 48) - 1;

    internal static string Create(DateTimeOffset backendUtc)
    {
        Span<byte> entropy = stackalloc byte[10];
        RandomNumberGenerator.Fill(entropy);
        try { return Create(backendUtc, entropy); }
        finally { CryptographicOperations.ZeroMemory(entropy); }
    }

    internal static string Create(DateTimeOffset backendUtc, ReadOnlySpan<byte> entropy)
    {
        if (backendUtc.Offset != TimeSpan.Zero || entropy.Length != 10)
        {
            throw new ArgumentException("An owner fence needs backend UTC and exactly 80 random bits.");
        }
        long milliseconds = backendUtc.ToUnixTimeMilliseconds();
        if (milliseconds < 0 || milliseconds > MaximumTimestamp)
        {
            throw new ArgumentOutOfRangeException(nameof(backendUtc));
        }

        Span<byte> bytes = stackalloc byte[16];
        for (int i = 5; i >= 0; i--)
        {
            bytes[i] = (byte)milliseconds;
            milliseconds >>= 8;
        }
        entropy.CopyTo(bytes[6..]);
        Span<char> encoded = stackalloc char[26];
        for (int digit = 0; digit < encoded.Length; digit++)
        {
            int value = 0;
            for (int bit = 0; bit < 5; bit++)
            {
                int sourceBit = digit * 5 + bit - 2;
                value <<= 1;
                if (sourceBit >= 0)
                {
                    value |= (bytes[sourceBit / 8] >> (7 - sourceBit % 8)) & 1;
                }
            }
            encoded[digit] = Alphabet[value];
        }
        CryptographicOperations.ZeroMemory(bytes);
        return new string(encoded);
    }

    internal static void RequireCanonical(string ownerFence)
    {
        ArgumentException.ThrowIfNullOrEmpty(ownerFence);
        if (ownerFence.Length != 26 || ownerFence[0] is < '0' or > '7')
        {
            throw new InvalidOperationException("CapabilityMismatch: owner fence is not a canonical 26-character ULID.");
        }
        foreach (char digit in ownerFence)
        {
            if (Alphabet.IndexOf(digit) < 0)
            {
                throw new InvalidOperationException("CapabilityMismatch: owner fence is not a canonical 26-character ULID.");
            }
        }
    }
}
