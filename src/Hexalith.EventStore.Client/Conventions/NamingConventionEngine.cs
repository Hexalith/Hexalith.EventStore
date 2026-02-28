
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;

using Hexalith.EventStore.Client.Attributes;

namespace Hexalith.EventStore.Client.Conventions;

/// <summary>
/// Single source of truth for deriving kebab-case domain names from .NET type names.
/// Supports automatic PascalCase-to-kebab-case conversion with suffix stripping,
/// attribute override via <see cref="EventStoreDomainAttribute"/>, and DAPR resource name derivation.
/// </summary>
public static class NamingConventionEngine {
    private static readonly string[] _knownSuffixes = ["Aggregate", "Projection", "Processor"];

    private static readonly Regex _wordBoundaryRegex = new(
        @"(?<=[a-z0-9])([A-Z])|(?<=[A-Z])([A-Z][a-z])|(?<=[a-zA-Z])([0-9])",
        RegexOptions.Compiled);

    private static readonly Regex _domainNameRegex = new(
        @"^[a-z0-9]([a-z0-9-]*[a-z0-9])?$",
        RegexOptions.Compiled);

    private static readonly ConcurrentDictionary<Type, string> _cache = new();

    /// <summary>
    /// Gets the domain name for the specified type. Returns the attribute value if
    /// <see cref="EventStoreDomainAttribute"/> is present, otherwise derives the name
    /// from the type name using PascalCase-to-kebab-case conversion with suffix stripping.
    /// </summary>
    /// <param name="type">The type to derive a domain name from.</param>
    /// <returns>A validated kebab-case domain name.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the derived or attribute-supplied name is invalid.</exception>
    public static string GetDomainName(Type type) {
        ArgumentNullException.ThrowIfNull(type);
        return _cache.GetOrAdd(type, static t => ResolveDomainName(t));
    }

    /// <summary>
    /// Gets the domain name for the specified type parameter.
    /// </summary>
    /// <typeparam name="T">The type to derive a domain name from.</typeparam>
    /// <returns>A validated kebab-case domain name.</returns>
    public static string GetDomainName<T>() => GetDomainName(typeof(T));

    /// <summary>
    /// Gets the DAPR state store name for the specified domain.
    /// </summary>
    /// <param name="domain">The validated domain name.</param>
    /// <returns>The state store name in the format "{domain}-eventstore".</returns>
    public static string GetStateStoreName(string domain) {
        ValidateDomainArgument(domain, nameof(domain));
        return $"{domain}-eventstore";
    }

    /// <summary>
    /// Gets the DAPR pub/sub topic for the specified tenant and domain.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="domain">The validated domain name.</param>
    /// <returns>The pub/sub topic in the format "{tenantId}.{domain}.events".</returns>
    public static string GetPubSubTopic(string tenantId, string domain) {
        ValidateDomainArgument(tenantId, nameof(tenantId));
        ValidateDomainArgument(domain, nameof(domain));
        return $"{tenantId}.{domain}.events";
    }

    /// <summary>
    /// Gets the DAPR command endpoint name for the specified domain.
    /// </summary>
    /// <param name="domain">The validated domain name.</param>
    /// <returns>The command endpoint name in the format "{domain}-commands".</returns>
    public static string GetCommandEndpoint(string domain) {
        ValidateDomainArgument(domain, nameof(domain));
        return $"{domain}-commands";
    }

    /// <summary>
    /// Clears the domain name resolution cache. Intended for test isolation.
    /// </summary>
    internal static void ClearCache() => _cache.Clear();

    private static string ResolveDomainName(Type type) {
        EventStoreDomainAttribute? attribute = type.GetCustomAttribute<EventStoreDomainAttribute>();
        if (attribute is not null) {
            string attributeValue = attribute.DomainName;
            ValidateDomainName(attributeValue, type);
            return attributeValue;
        }

        string typeName = type.Name;
        string stripped = StripKnownSuffix(typeName);

        if (stripped.Length == 0) {
            throw new ArgumentException(
                $"Type '{typeName}' produces an empty domain name after suffix stripping.", nameof(type));
        }

        string kebab = _wordBoundaryRegex.Replace(stripped, "-$1$2$3").ToLowerInvariant();
        ValidateDomainName(kebab, type);
        return kebab;
    }

    private static string StripKnownSuffix(string typeName) {
        foreach (string suffix in _knownSuffixes) {
            if (typeName.EndsWith(suffix, StringComparison.Ordinal)) {
                return typeName[..^suffix.Length];
            }
        }

        return typeName;
    }

    private static void ValidateDomainName(string name, Type type) {
        if (name.Length > 64) {
            throw new ArgumentException(
                $"Domain name derived from type '{type.Name}' exceeds 64 characters: '{name}'.");
        }

        if (!_domainNameRegex.IsMatch(name)) {
            throw new ArgumentException(
                $"Domain name '{name}' derived from type '{type.Name}' is invalid. " +
                "Must match ^[a-z0-9]([a-z0-9-]*[a-z0-9])?$ (lowercase alphanumeric + hyphens, no leading/trailing hyphens).");
        }
    }

    private static void ValidateDomainArgument(string value, string parameterName) {
        ArgumentNullException.ThrowIfNull(value);

        if (string.IsNullOrWhiteSpace(value)) {
            throw new ArgumentException($"{parameterName} cannot be empty or whitespace.", parameterName);
        }

        if (value.Length > 64) {
            throw new ArgumentException($"{parameterName} cannot exceed 64 characters. Got {value.Length}.", parameterName);
        }

        if (!_domainNameRegex.IsMatch(value)) {
            throw new ArgumentException(
                $"{parameterName} '{value}' is invalid. Must match ^[a-z0-9]([a-z0-9-]*[a-z0-9])?$ (lowercase alphanumeric + hyphens, no leading/trailing hyphens).",
                parameterName);
        }
    }
}
