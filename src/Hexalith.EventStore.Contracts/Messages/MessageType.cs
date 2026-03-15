using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Contracts.Messages;

/// <summary>
/// Strongly-typed value object representing a message type in the format {domain}-{name}-v{ver}.
/// Immutable, equatable, and serialized as a plain JSON string via <see cref="MessageTypeJsonConverter"/>.
/// </summary>
[JsonConverter(typeof(MessageTypeJsonConverter))]
public sealed partial record MessageType {
    /// <summary>
    /// Maximum total length of the canonical MessageType string.
    /// Domain max 64 + name max 120 + "-v" + version digits ≈ 192.
    /// </summary>
    public const int MaxLength = 192;

    [GeneratedRegex(@"^[a-z0-9]+$", RegexOptions.Compiled)]
    private static partial Regex DomainRegex();

    [GeneratedRegex(@"^[a-z0-9]([a-z0-9-]*[a-z0-9])?$", RegexOptions.Compiled)]
    private static partial Regex NameRegex();

    [GeneratedRegex(@"-v(\d+)$", RegexOptions.Compiled)]
    private static partial Regex VersionSuffixRegex();

    private MessageType(string domain, string name, int version) {
        Domain = domain;
        Name = name;
        Version = version;
    }

    /// <summary>
    /// Gets the domain segment (single kebab segment, no hyphens).
    /// </summary>
    public string Domain { get; }

    /// <summary>
    /// Gets the message name (one or more kebab segments).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the message version (>= 1).
    /// </summary>
    public int Version { get; }

    /// <summary>
    /// Parses a canonical message type string in the format {domain}-{name}-v{ver}.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <returns>A validated <see cref="MessageType"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when <paramref name="value"/> is not a valid message type.</exception>
    public static MessageType Parse(string value) {
        ArgumentNullException.ThrowIfNull(value);

        if (string.IsNullOrWhiteSpace(value)) {
            throw new FormatException("MessageType string cannot be empty or whitespace.");
        }

        if (value.Length > MaxLength) {
            throw new FormatException($"MessageType string exceeds maximum length of {MaxLength} characters. Got {value.Length}.");
        }

        // Extract version from the last -v{digits} suffix
        Match versionMatch = VersionSuffixRegex().Match(value);
        if (!versionMatch.Success) {
            throw new FormatException($"MessageType '{value}' is missing a valid version suffix (-v{{digits}}).");
        }

        int version = int.Parse(versionMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        if (version < 1) {
            throw new FormatException($"MessageType version must be >= 1. Got {version}.");
        }

        // Strip the version suffix to get {domain}-{name}
        string withoutVersion = value[..versionMatch.Index];

        // Find the first hyphen — everything before is domain
        int firstHyphen = withoutVersion.IndexOf('-');
        if (firstHyphen < 1) {
            throw new FormatException($"MessageType '{value}' is missing a domain segment before the first hyphen.");
        }

        string domain = withoutVersion[..firstHyphen];
        string name = withoutVersion[(firstHyphen + 1)..];

        // Validate domain — single segment, no hyphens
        if (!DomainRegex().IsMatch(domain)) {
            throw new FormatException($"MessageType domain '{domain}' is invalid. Must match ^[a-z0-9]+$ (lowercase alphanumeric, no hyphens).");
        }

        // Validate name — non-empty kebab-case
        if (string.IsNullOrEmpty(name)) {
            throw new FormatException($"MessageType '{value}' has an empty name segment.");
        }

        if (!NameRegex().IsMatch(name)) {
            throw new FormatException($"MessageType name '{name}' is invalid. Must be valid kebab-case (lowercase alphanumeric + hyphens, no leading/trailing hyphens).");
        }

        return new MessageType(domain, name, version);
    }

    /// <summary>
    /// Attempts to parse a message type string. Returns false for invalid inputs without throwing.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <param name="result">The parsed <see cref="MessageType"/>, or null if parsing fails.</param>
    /// <returns>True if parsing succeeded; false otherwise.</returns>
    public static bool TryParse(string? value, out MessageType? result) {
        result = null;
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }

        try {
            result = Parse(value);
            return true;
        }
        catch (FormatException) {
            return false;
        }
    }

    /// <summary>
    /// Assembles a <see cref="MessageType"/> from a domain name, .NET message type, and version.
    /// The type name is converted from PascalCase to kebab-case (no suffix stripping).
    /// </summary>
    /// <param name="domain">The domain name (lowercase, no hyphens).</param>
    /// <param name="messageType">The .NET type whose name will be converted to kebab-case.</param>
    /// <param name="version">The message version (must be >= 1).</param>
    /// <returns>A validated <see cref="MessageType"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="domain"/> or <paramref name="messageType"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when arguments are invalid.</exception>
    public static MessageType Assemble(string domain, Type messageType, int version) {
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(messageType);

        if (string.IsNullOrWhiteSpace(domain)) {
            throw new ArgumentException("Domain cannot be empty or whitespace.", nameof(domain));
        }

        if (version < 1) {
            throw new ArgumentException($"Version must be >= 1. Got {version}.", nameof(version));
        }

        string kebabName = KebabConverter.ConvertToKebab(messageType.Name);
        string canonical = $"{domain}-{kebabName}-v{version}";

        if (canonical.Length > MaxLength) {
            throw new ArgumentException($"Assembled MessageType exceeds maximum length of {MaxLength} characters: '{canonical}'.");
        }

        // Validate domain — single segment, no hyphens
        if (!DomainRegex().IsMatch(domain)) {
            throw new ArgumentException($"Domain '{domain}' is invalid. Must match ^[a-z0-9]+$ (lowercase alphanumeric, no hyphens).", nameof(domain));
        }

        // Validate generated name so Assemble cannot produce an invalid value object.
        if (!NameRegex().IsMatch(kebabName)) {
            throw new ArgumentException(
                $"Message type name '{messageType.Name}' converts to invalid kebab-case '{kebabName}'. MessageType names must contain only lowercase ASCII letters, digits, and hyphens.",
                nameof(messageType));
        }

        return new MessageType(domain, kebabName, version);
    }

    /// <summary>
    /// Returns the canonical string representation: {domain}-{name}-v{ver}.
    /// </summary>
    public override string ToString() => $"{Domain}-{Name}-v{Version}";
}
