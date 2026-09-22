using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Admin.Abstractions.Security;

/// <summary>
/// Shared substring-based detection for protected-data sentinels and credential-shaped tokens
/// used by CLI and MCP redaction paths. Source of truth for the marker vocabulary.
/// </summary>
public static partial class UnsafeMarkerDetection {
    private const int MaxJwtHeaderSegmentLength = 1024;
    private const int MaxPercentDecodePasses = 8;

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="value"/> contains a sentinel marker or
    /// a credential-shaped key=value token.
    /// </summary>
    /// <param name="value">String to inspect. Null/empty returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when an unsafe marker is detected.</returns>
    public static bool ContainsUnsafeMarker(string? value) {
        if (string.IsNullOrEmpty(value)) {
            return false;
        }

        return ContainsUnsafeMarkerCore(value) || ContainsUnsafeMarkerInPercentDecodedCopies(value);
    }

    [GeneratedRegex("""(?:^|[^A-Za-z0-9])Bearer\s+[A-Za-z0-9._~+/=-]+""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"(?:^|[^A-Za-z0-9_-])(?<header>[A-Za-z0-9_-]{8,})\.[A-Za-z0-9_-]{2,}\.[A-Za-z0-9_-]*(?:$|[^A-Za-z0-9_-])", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex JwtCandidateRegex();

    [GeneratedRegex("\"(?:access_token|accesstoken|refresh_token|refreshtoken|id_token|idtoken|token|password|secret|client_secret|clientsecret|api_key|apikey|authorization)\"\\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex JsonSecretFieldRegex();

    [GeneratedRegex(@"(?:^|[?&#;\s])(?:access_token|accesstoken|refresh_token|refreshtoken|id_token|idtoken|token|password|secret|client_secret|clientsecret|api_key|apikey|authorization|sig)\s*[=:]\s*[^\s&;#]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex QuerySecretRegex();

    [GeneratedRegex(@"(?:^|[?&#;\s])(?<name>(?:[A-Za-z0-9_]|%[0-9A-Fa-f]{2}){1,96})\s*[=:]\s*[^\s&;#]", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex PercentEncodedQueryNameRegex();

    [GeneratedRegex("%(?<hex>[0-9A-Fa-f]{2})", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex PercentEncodedByteRegex();

    [GeneratedRegex(@"\\u(?<hex>[0-9A-Fa-f]{4})", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex JsonUnicodeEscapeRegex();

    [GeneratedRegex(@"\b[a-z][a-z0-9+.-]*://[^\s/?#@]+(?:(?::|%3a)[^\s/?#@]*)?(?:@|%40)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex UriUserInfoRegex();

    private static bool ContainsUnsafeMarkerCore(string value)
        => value.Contains("PROTECTED_", StringComparison.OrdinalIgnoreCase)
            || value.Contains("AccountKey=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("SharedAccessKey=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Password=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("ConnectionString=", StringComparison.OrdinalIgnoreCase)
            || value.Contains(";ConnectionString=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Endpoint=sb://", StringComparison.OrdinalIgnoreCase)
            || BearerTokenRegex().IsMatch(value)
            || ContainsJsonWebToken(value)
            || ContainsJsonSecretField(value)
            || ContainsQuerySecret(value)
            || UriUserInfoRegex().IsMatch(value);

    private static bool ContainsQuerySecret(string value)
        => QuerySecretRegex().IsMatch(value) || ContainsPercentEncodedQuerySecret(value);

    private static bool ContainsJsonSecretField(string value) {
        if (JsonSecretFieldRegex().IsMatch(value)) {
            return true;
        }

        if (!value.Contains("\\u", StringComparison.Ordinal)) {
            return false;
        }

        string decoded = DecodeJsonUnicodeLetters(value);
        return !string.Equals(decoded, value, StringComparison.Ordinal)
            && JsonSecretFieldRegex().IsMatch(decoded);
    }

    private static bool ContainsUnsafeMarkerInPercentDecodedCopies(string value) {
        if (!value.Contains('%', StringComparison.Ordinal)) {
            return false;
        }

        string current = value;
        for (int pass = 0; pass < MaxPercentDecodePasses; pass++) {
            string decoded = DecodePercentEncodedCredentialCharacters(current);
            if (string.Equals(decoded, current, StringComparison.Ordinal)) {
                return false;
            }

            if (ContainsUnsafeMarkerCore(decoded)) {
                return true;
            }

            if (pass == MaxPercentDecodePasses - 1) {
                return true;
            }

            current = decoded;
        }

        return false;
    }

    private static string DecodePercentEncodedCredentialCharacters(string value)
        => PercentEncodedByteRegex().Replace(value, static match => {
            int code = Convert.ToInt32(match.Groups["hex"].Value, 16);
            return IsCredentialEncodingByte(code) ? ((char)code).ToString() : match.Value;
        });

    private static bool IsCredentialEncodingByte(int code)
        => code is '%' or '/' or '?' or '&' or '#' or ';' or '=' or ':' or '@' or '_' or '-' or '.' or ' '
            or (>= '0' and <= '9')
            or (>= 'A' and <= 'Z')
            or (>= 'a' and <= 'z');

    private static string DecodeJsonUnicodeLetters(string value)
        => JsonUnicodeEscapeRegex().Replace(value, static match => {
            int code = Convert.ToInt32(match.Groups["hex"].Value, 16);
            return code is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or '_'
                ? ((char)code).ToString()
                : match.Value;
        });

    private static bool ContainsJsonWebToken(string value) {
        foreach (Match match in JwtCandidateRegex().Matches(value)) {
            if (IsJsonWebTokenHeader(match.Groups["header"].Value)) {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsPercentEncodedQuerySecret(string value) {
        foreach (Match match in PercentEncodedQueryNameRegex().Matches(value)) {
            string name = match.Groups["name"].Value;
            if (!name.Contains('%')) {
                continue;
            }

            string decodedName = name;
            for (int pass = 0; pass < 2 && decodedName.Contains('%', StringComparison.Ordinal); pass++) {
                string next;
                try {
                    next = Uri.UnescapeDataString(decodedName);
                }
                catch (UriFormatException) {
                    break;
                }

                if (string.Equals(next, decodedName, StringComparison.Ordinal)) {
                    break;
                }

                decodedName = next;
            }

            string normalized = decodedName
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Trim('\0', ' ', '\t', '\r', '\n')
                .ToLowerInvariant();
            if (normalized is "accesstoken"
                or "refreshtoken"
                or "idtoken"
                or "token"
                or "password"
                or "secret"
                or "clientsecret"
                or "apikey"
                or "authorization"
                or "sig") {
                return true;
            }
        }

        return false;
    }

    private static bool IsJsonWebTokenHeader(string segment) {
        if (segment.Length > MaxJwtHeaderSegmentLength) {
            return true;
        }

        try {
            string normalized = segment.Replace('-', '+').Replace('_', '/');
            int paddingLength = (4 - (normalized.Length % 4)) % 4;
            if (paddingLength > 0) {
                normalized += new string('=', paddingLength);
            }

            byte[] bytes = Convert.FromBase64String(normalized);
            using JsonDocument document = JsonDocument.Parse(bytes);
            if (document.RootElement.ValueKind != JsonValueKind.Object) {
                return false;
            }

            if (document.RootElement.TryGetProperty("alg", out JsonElement algorithm)
                && algorithm.ValueKind == JsonValueKind.String) {
                return true;
            }

            return document.RootElement.TryGetProperty("typ", out JsonElement tokenType)
                && tokenType.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(tokenType.GetString());
        }
        catch (FormatException) {
            return false;
        }
        catch (JsonException) {
            return false;
        }
    }
}
