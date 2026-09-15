// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 6, 7, 14, and 15.
using System.Text;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Encodes strict NFC UTF-8 identity fields under the pdenc-v2 AAD rules.
/// </summary>
internal static class CanonicalText
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    /// <summary>
    /// Encodes and validates a canonical text field.
    /// </summary>
    internal static byte[] Encode(string? value, int minimumBytes, int maximumBytes)
    {
        _ = GetByteCount(value, minimumBytes, maximumBytes);
        try
        {
            return _strictUtf8.GetBytes(value!);
        }
        catch (EncoderFallbackException)
        {
            throw new PayloadProtectionFormatException();
        }
    }

    /// <summary>
    /// Validates canonical text and returns its strict UTF-8 byte count without allocating an encoded copy.
    /// </summary>
    internal static int GetByteCount(string? value, int minimumBytes, int maximumBytes)
    {
        if (value is null)
        {
            throw new PayloadProtectionFormatException();
        }

        if (value.Length > maximumBytes)
        {
            throw new PayloadProtectionFormatException();
        }

        try
        {
            if (!value.IsNormalized(NormalizationForm.FormC))
            {
                throw new PayloadProtectionFormatException();
            }
        }
        catch (ArgumentException)
        {
            throw new PayloadProtectionFormatException();
        }

        foreach (char character in value)
        {
            if (character is <= '\u001f' or '\u007f')
            {
                throw new PayloadProtectionFormatException();
            }
        }

        try
        {
            int byteCount = _strictUtf8.GetByteCount(value);
            if (byteCount < minimumBytes || byteCount > maximumBytes)
            {
                throw new PayloadProtectionFormatException();
            }

            return byteCount;
        }
        catch (EncoderFallbackException)
        {
            throw new PayloadProtectionFormatException();
        }
    }

    /// <summary>
    /// Decodes strict UTF-8 bytes and applies the same canonical validation.
    /// </summary>
    internal static string Decode(ReadOnlySpan<byte> value, int minimumBytes, int maximumBytes)
    {
        if (value.Length < minimumBytes || value.Length > maximumBytes)
        {
            throw new PayloadProtectionFormatException();
        }

        try
        {
            string decoded = _strictUtf8.GetString(value);
            _ = GetByteCount(decoded, minimumBytes, maximumBytes);
            return decoded;
        }
        catch (DecoderFallbackException)
        {
            throw new PayloadProtectionFormatException();
        }
    }

    /// <summary>
    /// Rejects a byte sequence that is not well-formed UTF-8 without materializing plaintext text.
    /// </summary>
    internal static void ValidateUtf8(ReadOnlySpan<byte> value)
    {
        try
        {
            _ = _strictUtf8.GetCharCount(value);
        }
        catch (DecoderFallbackException)
        {
            throw new PayloadProtectionFormatException();
        }
    }
}
