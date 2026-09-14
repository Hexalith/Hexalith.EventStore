using System.Text;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Encodes strict NFC UTF-8 identity fields under the pdenc-v2 AAD rules.
/// </summary>
internal static class CanonicalText {
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    /// <summary>
    /// Encodes and validates a canonical text field.
    /// </summary>
    internal static byte[] Encode(string? value, int minimumBytes, int maximumBytes) {
        if (value is null) {
            throw new PayloadProtectionFormatException();
        }

        try {
            if (!value.IsNormalized(NormalizationForm.FormC)) {
                throw new PayloadProtectionFormatException();
            }
        }
        catch (ArgumentException) {
            throw new PayloadProtectionFormatException();
        }

        foreach (char character in value) {
            if (character is <= '\u001f' or '\u007f') {
                throw new PayloadProtectionFormatException();
            }
        }

        byte[] encoded;
        try {
            encoded = _strictUtf8.GetBytes(value);
        }
        catch (EncoderFallbackException) {
            throw new PayloadProtectionFormatException();
        }

        if (encoded.Length < minimumBytes || encoded.Length > maximumBytes) {
            throw new PayloadProtectionFormatException();
        }

        return encoded;
    }

    /// <summary>
    /// Decodes strict UTF-8 bytes and applies the same canonical validation.
    /// </summary>
    internal static string Decode(ReadOnlySpan<byte> value, int minimumBytes, int maximumBytes) {
        if (value.Length < minimumBytes || value.Length > maximumBytes) {
            throw new PayloadProtectionFormatException();
        }

        try {
            string decoded = _strictUtf8.GetString(value);
            _ = Encode(decoded, minimumBytes, maximumBytes);
            return decoded;
        }
        catch (DecoderFallbackException) {
            throw new PayloadProtectionFormatException();
        }
    }
}
