using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Contracts.Commands;

/// <summary>Shared command receiver and sender normalization of explicit domain-service versions.</summary>
public static partial class DomainServiceVersion
{
    /// <summary>Canonical command extension key.</summary>
    public const string ExtensionKey = "domain-service-version";

    /// <summary>Returns a validated lowercase explicit override, or the supplied committed/default version when absent.</summary>
    public static string Resolve(CommandEnvelope command, string defaultVersion = "v1")
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Extensions is null || !command.Extensions.TryGetValue(ExtensionKey, out string? value)
            || string.IsNullOrWhiteSpace(value)) return defaultVersion;
        string normalized = value.ToLowerInvariant();
        Validate(normalized);
        return normalized;
    }

    /// <summary>Preserves the established v-number version grammar and validation error.</summary>
    public static void Validate(string version)
    {
        if (!VersionFormatRegex().IsMatch(version))
            throw new ArgumentException($"Invalid domain service version format '{version}'. Version must match pattern 'v{{number}}' (e.g., v1, v2, v10).", nameof(version));
    }

    [GeneratedRegex(@"^v[0-9]+$")]
    private static partial Regex VersionFormatRegex();
}
