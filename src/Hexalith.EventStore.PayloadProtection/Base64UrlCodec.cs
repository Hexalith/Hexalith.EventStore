namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Implements canonical unpadded RFC 4648 base64url for pdenc-v2 envelopes (normative section 6.3).
/// </summary>
internal static class Base64UrlCodec {
    /// <summary>
    /// Encodes bytes without padding.
    /// </summary>
    internal static string Encode(ReadOnlySpan<byte> value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// Decodes a bounded value and rejects every non-canonical spelling.
    /// </summary>
    internal static byte[] Decode(string? value) {
        if (value is null
            || value.Length > PayloadProtectionLimits.EnvelopeTextCharacters
            || value.Length % 4 == 1) {
            throw new PayloadProtectionFormatException();
        }

        foreach (char character in value) {
            if (!(character is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '-'
                or '_')) {
                throw new PayloadProtectionFormatException();
            }
        }

        string padded = value.Replace('-', '+').Replace('_', '/');
        padded += (value.Length % 4) switch {
            2 => "==",
            3 => "=",
            _ => string.Empty,
        };

        byte[] decoded;
        try {
            decoded = Convert.FromBase64String(padded);
        }
        catch (FormatException) {
            throw new PayloadProtectionFormatException();
        }

        if (decoded.Length > PayloadProtectionLimits.EnvelopeBytes
            || !string.Equals(value, Encode(decoded), StringComparison.Ordinal)) {
            throw new PayloadProtectionFormatException();
        }

        return decoded;
    }
}
