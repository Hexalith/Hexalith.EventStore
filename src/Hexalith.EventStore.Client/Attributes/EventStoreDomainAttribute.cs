
namespace Hexalith.EventStore.Client.Attributes;

/// <summary>
/// Overrides the convention-derived domain name for an aggregate or projection class.
/// When applied, NamingConventionEngine returns this attribute's value instead of
/// deriving the name from the type name.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EventStoreDomainAttribute : Attribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="EventStoreDomainAttribute"/> class.
    /// </summary>
    /// <param name="domainName">The explicit domain name to use. Must be non-empty and non-whitespace.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="domainName"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="domainName"/> is empty or whitespace.</exception>
    public EventStoreDomainAttribute(string domainName) {
        ArgumentNullException.ThrowIfNull(domainName);
        if (string.IsNullOrWhiteSpace(domainName)) {
            throw new ArgumentException("Domain name cannot be empty or whitespace.", nameof(domainName));
        }

        DomainName = domainName;
    }

    /// <summary>Gets the explicit domain name.</summary>
    public string DomainName { get; }
}
