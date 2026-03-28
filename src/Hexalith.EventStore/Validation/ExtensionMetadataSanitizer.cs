
using System.Text;
using System.Text.RegularExpressions;

using Hexalith.EventStore.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Validation;
/// <summary>
/// Sanitizes extension metadata at the API gateway to prevent injection attacks (SEC-4).
/// Validates size limits, character sets, and injection patterns.
/// Separate from structural validation (SubmitCommandRequestValidator).
/// </summary>
public partial class ExtensionMetadataSanitizer(IOptions<ExtensionMetadataOptions> options) {
    private readonly ExtensionMetadataOptions _options = options.Value;

    /// <summary>
    /// Sanitizes extension metadata, rejecting oversized, malformed, or injection-bearing extensions.
    /// </summary>
    /// <param name="extensions">The extension metadata to sanitize. Null or empty is valid.</param>
    /// <returns>A <see cref="SanitizeResult"/> indicating success or failure with reason.</returns>
    public SanitizeResult Sanitize(IDictionary<string, string>? extensions) {
        if (extensions is null || extensions.Count == 0) {
            return SanitizeResult.Success();
        }

        // Check extension count
        if (extensions.Count > _options.MaxExtensionCount) {
            return SanitizeResult.Failure(
                $"Extension count {extensions.Count} exceeds maximum of {_options.MaxExtensionCount}.");
        }

        int totalSize = 0;

        foreach (KeyValuePair<string, string> kvp in extensions) {
            // Check individual key length
            if (kvp.Key.Length > _options.MaxKeyLength) {
                return SanitizeResult.Failure(
                    $"Extension key length {kvp.Key.Length} exceeds maximum of {_options.MaxKeyLength}.");
            }

            // Check individual value length
            if (kvp.Value.Length > _options.MaxValueLength) {
                return SanitizeResult.Failure(
                    $"Extension value length {kvp.Value.Length} exceeds maximum of {_options.MaxValueLength}.");
            }

            // Validate key character set: printable ASCII identifiers only [a-zA-Z0-9_.-]
            if (!KeyPattern().IsMatch(kvp.Key)) {
                return SanitizeResult.Failure("Extension key contains invalid characters.");
            }

            // Validate value: reject control characters (\x00-\x1F except \t \n \r)
            if (ContainsControlCharacters(kvp.Value)) {
                return SanitizeResult.Failure("Extension value contains control characters.");
            }

            // Check for injection patterns in values
            if (XssPattern().IsMatch(kvp.Value)) {
                return SanitizeResult.Failure("Extension value contains XSS injection pattern.");
            }

            if (SqlInjectionPattern().IsMatch(kvp.Value)) {
                return SanitizeResult.Failure("Extension value contains SQL injection pattern.");
            }

            if (LdapInjectionPattern().IsMatch(kvp.Value)) {
                return SanitizeResult.Failure("Extension value contains LDAP injection pattern.");
            }

            if (PathTraversalPattern().IsMatch(kvp.Value)) {
                return SanitizeResult.Failure("Extension value contains path traversal pattern.");
            }

            totalSize += Encoding.UTF8.GetByteCount(kvp.Key) + Encoding.UTF8.GetByteCount(kvp.Value);
        }

        // Check total size
        if (totalSize > _options.MaxTotalSizeBytes) {
            return SanitizeResult.Failure(
                $"Total extension size {totalSize} bytes exceeds maximum of {_options.MaxTotalSizeBytes} bytes.");
        }

        return SanitizeResult.Success();
    }

    private static bool ContainsControlCharacters(string value) {
        foreach (char c in value) {
            // Reject control characters \x00-\x1F except \t (0x09), \n (0x0A), \r (0x0D)
            if (c is < (char)0x20 and not '\t' and not '\n' and not '\r') {
                return true;
            }
        }

        return false;
    }

    // Key pattern: alphanumeric + dots, hyphens, underscores
    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9._-]*$", RegexOptions.Compiled)]
    private static partial Regex KeyPattern();

    // XSS patterns
    [GeneratedRegex(@"(?i)(<\s*script|javascript\s*:|on\w+\s*=|<\s*iframe|<\s*object|<\s*embed)", RegexOptions.Compiled)]
    private static partial Regex XssPattern();

    // SQL injection patterns
    [GeneratedRegex(@"(?i)(';\s*(DROP|ALTER|DELETE|INSERT|UPDATE|EXEC)|UNION\s+SELECT|--\s*$)", RegexOptions.Compiled)]
    private static partial Regex SqlInjectionPattern();

    // LDAP injection patterns
    [GeneratedRegex(@"(\)\(|\*\)\(|\|\(|&\()", RegexOptions.Compiled)]
    private static partial Regex LdapInjectionPattern();

    // Path traversal patterns
    [GeneratedRegex(@"(\.\./|\.\.\\)", RegexOptions.Compiled)]
    private static partial Regex PathTraversalPattern();
}

/// <summary>
/// Result of extension metadata sanitization.
/// </summary>
/// <param name="IsSuccess">Whether sanitization passed.</param>
/// <param name="RejectionReason">The reason for rejection, or null if successful.</param>
public record SanitizeResult(bool IsSuccess, string? RejectionReason) {
    public static SanitizeResult Success() => new(true, null);

    public static SanitizeResult Failure(string reason) => new(false, reason);
}
