using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Admin.Abstractions.Security;

/// <summary>
/// Shared substring-based detection for protected-data sentinels and credential-shaped tokens
/// used by CLI and MCP redaction paths. Source of truth for the marker vocabulary.
/// </summary>
public static partial class UnsafeMarkerDetection {
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
            || JwtRegex().IsMatch(value)
            || JsonSecretFieldRegex().IsMatch(value)
            || ClientSecretRegex().IsMatch(value)
            || UriUserInfoRegex().IsMatch(value);
    }

    [GeneratedRegex("""(?:^|[\s"':=])Bearer\s+[A-Za-z0-9._~+/=-]+""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"(?:^|[^A-Za-z0-9_-])[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}(?:$|[^A-Za-z0-9_-])", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex JwtRegex();

    [GeneratedRegex("\"(?:access_token|refresh_token|id_token|token|password|secret|client_secret|api_key|apikey|authorization)\"\\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex JsonSecretFieldRegex();

    [GeneratedRegex(@"(?:^|[?&;\s])client_secret\s*[=:]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex ClientSecretRegex();

    [GeneratedRegex(@"\b[a-z][a-z0-9+.-]*://[^\s/@:]+:[^\s/@]+@", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex UriUserInfoRegex();
}
