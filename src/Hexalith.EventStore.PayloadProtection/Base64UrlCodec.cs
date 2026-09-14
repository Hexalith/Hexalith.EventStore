namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Implements canonical unpadded RFC 4648 base64url for pdenc-v2 envelopes (normative section 6.3).
/// </summary>
internal static class Base64UrlCodec
{
    /// <summary>
    /// Encodes bytes without padding.
    /// </summary>
    internal static string Encode(ReadOnlySpan<byte> value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// Decodes a bounded string after rejecting oversize before scanning or copying it.
    /// </summary>
    internal static byte[] Decode(string? value)
    {
        if (value is null || value.Length > PayloadProtectionLimits.EnvelopeTextCharacters)
        {
            throw new PayloadProtectionFormatException();
        }

        return DecodeCharacters(value.AsSpan());
    }

    /// <summary>
    /// Decodes bounded ASCII JSON string contents without first constructing a managed carrier string.
    /// </summary>
    internal static byte[] Decode(ReadOnlySpan<byte> value)
    {
        if (value.Length > PayloadProtectionLimits.EnvelopeTextCharacters)
        {
            throw new PayloadProtectionFormatException();
        }

        char[] characters = new char[value.Length];
        try
        {
            for (int index = 0; index < value.Length; index++)
            {
                byte character = value[index];
                if (character > 0x7f)
                {
                    throw new PayloadProtectionFormatException();
                }

                characters[index] = (char)character;
            }

            return DecodeCharacters(characters);
        }
        finally
        {
            Array.Clear(characters);
        }
    }

    private static byte[] DecodeCharacters(ReadOnlySpan<char> value)
    {
        if (value.Length % 4 == 1)
        {
            throw new PayloadProtectionFormatException();
        }

        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (!(character is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '-'
                or '_'))
            {
                throw new PayloadProtectionFormatException();
            }
        }

        int paddingLength = (value.Length % 4) switch
        {
            2 => 2,
            3 => 1,
            _ => 0,
        };
        int paddedLength = checked(value.Length + paddingLength);
        int decodedLength = checked((value.Length / 4 * 3) + ((value.Length % 4) switch
        {
            2 => 1,
            3 => 2,
            _ => 0,
        }));
        if (decodedLength > PayloadProtectionLimits.EnvelopeBytes)
        {
            throw new PayloadProtectionFormatException();
        }

        char[] padded = new char[paddedLength];
        try
        {
            for (int index = 0; index < value.Length; index++)
            {
                padded[index] = value[index] switch
                {
                    '-' => '+',
                    '_' => '/',
                    _ => value[index],
                };
            }
            for (int index = value.Length; index < padded.Length; index++)
            {
                padded[index] = '=';
            }

            byte[] decoded = new byte[decodedLength];
            if (!Convert.TryFromBase64Chars(padded, decoded, out int bytesWritten)
                || bytesWritten != decoded.Length)
            {
                Array.Clear(decoded);
                throw new PayloadProtectionFormatException();
            }

            string canonical = Encode(decoded);
            if (canonical.Length != value.Length)
            {
                Array.Clear(decoded);
                throw new PayloadProtectionFormatException();
            }

            for (int index = 0; index < value.Length; index++)
            {
                if (canonical[index] != value[index])
                {
                    Array.Clear(decoded);
                    throw new PayloadProtectionFormatException();
                }
            }

            return decoded;
        }
        finally
        {
            Array.Clear(padded);
        }
    }
}
