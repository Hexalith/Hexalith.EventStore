namespace Hexalith.EventStore.Contracts.Identity;

/// <summary>
/// Provides static methods for parsing canonical identity strings and state store keys
/// into <see cref="AggregateIdentity"/> instances.
/// </summary>
public static class IdentityParser {
    /// <summary>
    /// Parses a canonical colon-separated string into an <see cref="AggregateIdentity"/>.
    /// </summary>
    /// <param name="canonical">The canonical string in format "tenantId:domain:aggregateId".</param>
    /// <returns>An <see cref="AggregateIdentity"/> parsed from the canonical string.</returns>
    /// <exception cref="FormatException">Thrown when the input is null, empty, or does not contain exactly 3 colon-separated segments.</exception>
    public static AggregateIdentity Parse(string canonical) {
        if (string.IsNullOrWhiteSpace(canonical)) {
            throw new FormatException("Canonical identity string cannot be null, empty, or whitespace.");
        }

        string[] segments = canonical.Split(':');

        if (segments.Length != 3) {
            throw new FormatException($"Canonical identity string must contain exactly 3 colon-separated segments (tenantId:domain:aggregateId). Got {segments.Length} segments from '{canonical}'.");
        }

        try {
            return new AggregateIdentity(segments[0], segments[1], segments[2]);
        }
        catch (ArgumentException ex) {
            throw new FormatException($"Failed to parse canonical identity string '{canonical}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Attempts to parse a canonical colon-separated string into an <see cref="AggregateIdentity"/>.
    /// </summary>
    /// <param name="canonical">The canonical string in format "tenantId:domain:aggregateId".</param>
    /// <param name="identity">When this method returns true, contains the parsed identity; otherwise, null.</param>
    /// <returns>true if parsing succeeded; otherwise, false.</returns>
    public static bool TryParse(string? canonical, out AggregateIdentity? identity) {
        identity = null;

        if (string.IsNullOrWhiteSpace(canonical)) {
            return false;
        }

        string[] segments = canonical.Split(':');

        if (segments.Length != 3) {
            return false;
        }

        try {
            identity = new AggregateIdentity(segments[0], segments[1], segments[2]);
            return true;
        }
        catch (ArgumentException) {
            return false;
        }
    }

    /// <summary>
    /// Parses a state store key into an <see cref="AggregateIdentity"/> and the remaining suffix.
    /// </summary>
    /// <param name="key">A state store key such as "acme:payments:order-123:events:5".</param>
    /// <returns>A tuple of the parsed <see cref="AggregateIdentity"/> and the suffix string after the identity portion.</returns>
    /// <exception cref="FormatException">Thrown when the key does not contain at least 4 colon-separated segments.</exception>
    public static (AggregateIdentity Identity, string Suffix) ParseStateStoreKey(string key) {
        if (string.IsNullOrWhiteSpace(key)) {
            throw new FormatException("State store key cannot be null, empty, or whitespace.");
        }

        string[] segments = key.Split(':');

        if (segments.Length < 4) {
            throw new FormatException($"State store key must contain at least 4 colon-separated segments (tenantId:domain:aggregateId:suffix...). Got {segments.Length} segments from '{key}'.");
        }

        try {
            var identity = new AggregateIdentity(segments[0], segments[1], segments[2]);
            string suffix = string.Join(':', segments[3..]);
            return (identity, suffix);
        }
        catch (ArgumentException ex) {
            throw new FormatException($"Failed to parse state store key '{key}': {ex.Message}", ex);
        }
    }
}
