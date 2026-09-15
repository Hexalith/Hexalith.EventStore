// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 8, 14, and 15.
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Supplies production CSPRNG bytes and canonical ULID references for a single protection attempt.
/// </summary>
internal sealed class CryptographicPayloadProtectionEntropy : IPayloadProtectionEntropy
{
    /// <inheritdoc/>
    public string CreateKeyReference()
    {
        Span<byte> value = stackalloc byte[16];
        Span<byte> timestampBytes = stackalloc byte[8];
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        BinaryPrimitives.WriteUInt64BigEndian(timestampBytes, checked((ulong)timestamp));
        timestampBytes[2..].CopyTo(value);
        RandomNumberGenerator.Fill(value[6..]);

        Span<char> encoded = stackalloc char[26];
        for (int characterIndex = 0; characterIndex < encoded.Length; characterIndex++)
        {
            int symbol = 0;
            for (int bitIndex = 0; bitIndex < 5; bitIndex++)
            {
                symbol <<= 1;
                int sourceBit = (characterIndex * 5) + bitIndex - 2;
                if (sourceBit >= 0)
                {
                    symbol |= (value[sourceBit / 8] >> (7 - (sourceBit & 7))) & 1;
                }
            }

            encoded[characterIndex] = PayloadProtectionWireFormat.CrockfordBase32Alphabet[symbol];
        }

        CryptographicOperations.ZeroMemory(value);
        return new string(encoded);
    }

    /// <inheritdoc/>
    public void FillDataEncryptionKey(Span<byte> destination)
        => RandomNumberGenerator.Fill(destination);
}
