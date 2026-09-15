// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 6, 14, and 15.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Implements canonical unpadded RFC 4648 base64url for pdenc-v2 envelopes (normative section 6.3).
/// </summary>
internal static class Base64UrlCodec
{
    /// <summary>
    /// Gets the exact unpadded base64url character count for a bounded byte count.
    /// </summary>
    internal static int GetEncodedLength(int byteCount)
    {
        if (byteCount < 0)
        {
            throw new PayloadProtectionFormatException();
        }

        return checked((byteCount / 3 * 4) + ((byteCount % 3) switch
        {
            1 => 2,
            2 => 3,
            _ => 0,
        }));
    }

    /// <summary>
    /// Encodes bytes without padding into a single zeroed staging buffer.
    /// </summary>
    internal static string Encode(ReadOnlySpan<byte> value)
    {
        int length = GetEncodedLength(value.Length);
        int paddedLength = checked((value.Length + 2) / 3 * 4);
        char[] padded = new char[paddedLength];
        try
        {
            if (!Convert.TryToBase64Chars(value, padded, out int charactersWritten)
                || charactersWritten != paddedLength)
            {
                throw new PayloadProtectionFormatException();
            }

            for (int index = 0; index < length; index++)
            {
                padded[index] = padded[index] switch
                {
                    '+' => '-',
                    '/' => '_',
                    _ => padded[index],
                };
            }

            return new string(padded, 0, length);
        }
        finally
        {
            Array.Clear(padded);
        }
    }

    /// <summary>
    /// Decodes a bounded string after rejecting oversize before scanning or copying it.
    /// </summary>
    /// <remarks>
    /// Ownership of the returned buffer transfers to the caller, which must zero it on every exit.
    /// Every staging buffer allocated here is cleared before returning.
    /// </remarks>
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

            byte[]? decoded = new byte[decodedLength];
            try
            {
                if (!Convert.TryFromBase64Chars(padded, decoded, out int bytesWritten)
                    || bytesWritten != decoded.Length)
                {
                    throw new PayloadProtectionFormatException();
                }

                if (!Convert.TryToBase64Chars(decoded, padded, out int charactersWritten)
                    || charactersWritten != padded.Length)
                {
                    throw new PayloadProtectionFormatException();
                }

                for (int index = 0; index < value.Length; index++)
                {
                    char canonical = padded[index] switch
                    {
                        '+' => '-',
                        '/' => '_',
                        _ => padded[index],
                    };
                    if (canonical != value[index])
                    {
                        throw new PayloadProtectionFormatException();
                    }
                }

                byte[] result = decoded;
                decoded = null;
                return result;
            }
            finally
            {
                if (decoded is not null)
                {
                    Array.Clear(decoded);
                }
            }
        }
        finally
        {
            Array.Clear(padded);
        }
    }
}
