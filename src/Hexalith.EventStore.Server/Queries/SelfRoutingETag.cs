
using System.Text;

namespace Hexalith.EventStore.Server.Queries;

/// <summary>
/// Utility for encoding and decoding self-routing ETags.
/// Format: {base64url(projectionType)}.{base64url-guid}.
/// The projection type is embedded in the ETag so the query endpoint
/// can determine the correct ETag actor without server-side routing state.
/// </summary>
internal static class SelfRoutingETag
{
    /// <summary>
    /// Encodes a projection type and GUID part into a self-routing ETag.
    /// </summary>
    /// <param name="projectionType">The projection type name.</param>
    /// <param name="guid">The base64url-encoded GUID portion.</param>
    /// <returns>A self-routing ETag string in the format {base64url(projectionType)}.{guid}.</returns>
    public static string Encode(string projectionType, string guid)
    {
        string encodedType = EncodeProjectionType(projectionType);
        return $"{encodedType}.{guid}";
    }

    /// <summary>
    /// Generates a new self-routing ETag with a fresh GUID for the given projection type.
    /// </summary>
    /// <param name="projectionType">The projection type name.</param>
    /// <returns>A self-routing ETag string.</returns>
    public static string GenerateNew(string projectionType)
    {
        byte[] bytes = Guid.NewGuid().ToByteArray();
        string guid = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        return Encode(projectionType, guid);
    }

    /// <summary>
    /// Attempts to decode a self-routing ETag into its projection type and GUID parts.
    /// Returns false for malformed ETags, old-format ETags, and invalid base64url.
    /// </summary>
    /// <param name="etag">The ETag string to decode.</param>
    /// <param name="projectionType">The decoded projection type, or null if decode fails.</param>
    /// <param name="guidPart">The GUID portion of the ETag, or null if decode fails.</param>
    /// <returns>True if the ETag was successfully decoded; false otherwise.</returns>
    public static bool TryDecode(string? etag, out string? projectionType, out string? guidPart)
    {
        projectionType = null;
        guidPart = null;

        if (string.IsNullOrEmpty(etag))
        {
            return false;
        }

        int dotIndex = etag.IndexOf('.');
        if (dotIndex <= 0 || dotIndex >= etag.Length - 1)
        {
            return false;
        }

        string encodedPrefix = etag[..dotIndex];
        guidPart = etag[(dotIndex + 1)..];

        if (string.IsNullOrEmpty(guidPart))
        {
            return false;
        }

        try
        {
            string decoded = DecodeProjectionType(encodedPrefix);
            if (string.IsNullOrEmpty(decoded) || decoded.Contains(':'))
            {
                guidPart = null;
                return false;
            }

            projectionType = decoded;
            return true;
        }
        catch (FormatException)
        {
            guidPart = null;
            return false;
        }
    }

    private static string EncodeProjectionType(string projectionType)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(projectionType);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string DecodeProjectionType(string encoded)
    {
        string padded = encoded.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
            case 1:
                // length % 4 == 1 is always invalid base64
                throw new FormatException("Invalid base64 length.");
        }

        byte[] bytes = Convert.FromBase64String(padded);
        return Encoding.UTF8.GetString(bytes);
    }
}
