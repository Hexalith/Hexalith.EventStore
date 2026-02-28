
namespace Hexalith.EventStore.Client.Discovery;

/// <summary>
/// Represents a single domain type discovered by the <see cref="AssemblyScanner"/>.
/// </summary>
/// <param name="Type">The discovered CLR type.</param>
/// <param name="DomainName">The resolved domain name (from <c>NamingConventionEngine</c>).</param>
/// <param name="StateType">The <c>TState</c> or <c>TReadModel</c> generic argument extracted from the base class.</param>
/// <param name="Kind">Whether this type is an aggregate or a projection.</param>
public sealed record DiscoveredDomain(
    Type Type,
    string DomainName,
    Type StateType,
    DomainKind Kind);
