namespace Hexalith.EventStore.Admin.Abstractions.Security;

/// <summary>
/// Shared substring-based detection for protected-data sentinels and credential-shaped tokens
/// used by CLI and MCP redaction paths. Source of truth for the marker vocabulary.
/// </summary>
public static class UnsafeMarkerDetection {
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
            || value.Contains("Endpoint=sb://", StringComparison.OrdinalIgnoreCase);
    }
}
