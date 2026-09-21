using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Admin.Abstractions.Security;

/// <summary>
/// Shared substring-based detection for protected-data sentinels and credential-shaped tokens
/// used by CLI and MCP redaction paths. Source of truth for the marker vocabulary.
/// </summary>
public static partial class UnsafeMarkerDetection {
    private const int MaxJwtHeaderSegmentLength = 1024;

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

        return value.Contains("PROTECTED_", StringComparison.OrdinalIgnoreCase)
            || value.Contains("AccountKey=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("SharedAccessKey=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Password=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("ConnectionString=", StringComparison.OrdinalIgnoreCase)
            || value.Contains(";ConnectionString=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Endpoint=sb://", StringComparison.OrdinalIgnoreCase)
            || BearerTokenRegex().IsMatch(value)
            || ContainsJsonWebToken(value)
            || JsonSecretFieldRegex().IsMatch(value)
            || QuerySecretRegex().IsMatch(value)
            || UriUserInfoRegex().IsMatch(value);
    }

    [GeneratedRegex("""(?:^|[\s"':=])Bearer\s+[A-Za-z0-9._~+/=-]+""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"(?:^|[^A-Za-z0-9_-])(?<header>[A-Za-z0-9_-]{8,})\.[A-Za-z0-9_-]{2,}\.[A-Za-z0-9_-]{8,}(?:$|[^A-Za-z0-9_-])", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex JwtCandidateRegex();

    [GeneratedRegex("\"(?:access_token|accesstoken|refresh_token|refreshtoken|id_token|idtoken|token|password|secret|client_secret|clientsecret|api_key|apikey|authorization)\"\\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex JsonSecretFieldRegex();

    [GeneratedRegex(@"(?:^|[?&#;\s])(?:access_token|accesstoken|refresh_token|refreshtoken|id_token|idtoken|token|password|secret|client_secret|clientsecret|api_key|apikey|authorization)\s*[=:]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex QuerySecretRegex();

    [GeneratedRegex(@"\b[a-z][a-z0-9+.-]*://[^\s/?#@]+(?:(?::|%3a)[^\s/?#@]*)?(?:@|%40)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex UriUserInfoRegex();

    private static bool ContainsJsonWebToken(string value) {
        foreach (Match match in JwtCandidateRegex().Matches(value)) {
            if (IsJsonWebTokenHeader(match.Groups["header"].Value)) {
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
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("alg", out JsonElement algorithm)
                && algorithm.ValueKind == JsonValueKind.String;
        }
        catch (FormatException) {
            return false;
        }
        catch (JsonException) {
            return false;
        }
    }
}
