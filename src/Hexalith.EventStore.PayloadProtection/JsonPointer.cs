using System.Globalization;
using System.Text;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Implements the restricted RFC 6901 profile from normative section 7.2.
/// </summary>
internal static class JsonPointer
{
    /// <summary>
    /// Validates and decodes a pointer into reference tokens.
    /// </summary>
    internal static IReadOnlyList<string> Decode(
        string pointer,
        bool allowRoot,
        CancellationToken cancellationToken = default,
        Action<int>? checkpoint = null)
    {
        ArgumentNullException.ThrowIfNull(pointer);
        cancellationToken.ThrowIfCancellationRequested();
        _ = CanonicalText.GetByteCount(pointer, allowRoot ? 0 : 1, PayloadProtectionLimits.PathBytes);
        cancellationToken.ThrowIfCancellationRequested();
        if (pointer.Length == 0)
        {
            if (!allowRoot)
            {
                throw new PayloadProtectionFormatException();
            }

            cancellationToken.ThrowIfCancellationRequested();
            return [];
        }

        if (pointer[0] != '/')
        {
            throw new PayloadProtectionFormatException();
        }

        string[] encoded = pointer[1..].Split('/');
        var decoded = new string[encoded.Length];
        int examinedBytes = 0;
        CheckCancellation(ref examinedBytes, 1, cancellationToken, checkpoint);
        for (int index = 0; index < encoded.Length; index++)
        {
            if (index > 0)
            {
                CheckCancellation(ref examinedBytes, 1, cancellationToken, checkpoint);
            }

            var builder = new StringBuilder(encoded[index].Length);
            for (int offset = 0; offset < encoded[index].Length; offset++)
            {
                char character = encoded[index][offset];
                CheckCancellation(
                    ref examinedBytes,
                    GetUtf8ByteCount(character),
                    cancellationToken,
                    checkpoint);
                if (character != '~')
                {
                    _ = builder.Append(character);
                    continue;
                }

                if (++offset >= encoded[index].Length)
                {
                    throw new PayloadProtectionFormatException();
                }

                CheckCancellation(ref examinedBytes, 1, cancellationToken, checkpoint);
                _ = encoded[index][offset] switch
                {
                    '0' => builder.Append('~'),
                    '1' => builder.Append('/'),
                    _ => throw new PayloadProtectionFormatException(),
                };
            }

            decoded[index] = builder.ToString();
        }

        cancellationToken.ThrowIfCancellationRequested();
        return decoded;
    }

    private static void CheckCancellation(
        ref int examinedBytes,
        int byteCount,
        CancellationToken cancellationToken,
        Action<int>? checkpoint)
    {
        for (int index = 0; index < byteCount; index++)
        {
            examinedBytes = checked(examinedBytes + 1);
            if (examinedBytes == 1 || (examinedBytes & 255) == 0)
            {
                checkpoint?.Invoke(examinedBytes);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    private static int GetUtf8ByteCount(char character)
        => character switch
        {
            <= '\u007f' => 1,
            <= '\u07ff' => 2,
            >= '\udc00' and <= '\udfff' => 0,
            >= '\ud800' and <= '\udbff' => 4,
            _ => 3,
        };

    /// <summary>
    /// Parses one canonical array index token.
    /// </summary>
    internal static int ParseArrayIndex(string segment)
    {
        if (segment.Length == 0
            || (segment.Length > 1 && segment[0] == '0')
            || !int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out int result)
            || result < 0)
        {
            throw new PayloadProtectionFormatException();
        }

        return result;
    }
}
