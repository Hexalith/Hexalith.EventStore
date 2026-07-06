using System.Reflection;

using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Builds the operational-index metadata (commands, events, projections per domain) that the EventStore
/// gateway requests from a domain service via <c>POST /admin/operational-index-metadata</c>.
/// </summary>
public static class AdminOperationalIndexMetadata {
    /// <summary>
    /// Creates the operational-index metadata response for the requested domains (all discovered
    /// aggregate domains when none are explicitly requested).
    /// </summary>
    /// <param name="discovery">The discovery result for the hosted domains.</param>
    /// <param name="requestedDomains">The domains to include, or <c>null</c>/empty for all.</param>
    /// <param name="queryHandlers">
    /// The registered <see cref="IDomainQueryHandler"/> instances, used to report each domain's
    /// handler-served query types; <c>null</c> when the domain serves no queries via handlers.
    /// </param>
    /// <returns>The operational-index metadata response.</returns>
    public static Response Create(
        DiscoveryResult discovery,
        IReadOnlyList<string>? requestedDomains,
        IEnumerable<IDomainQueryHandler>? queryHandlers = null) {
        ArgumentNullException.ThrowIfNull(discovery);

        HashSet<string> requested = requestedDomains is { Count: > 0 }
            ? new HashSet<string>(requestedDomains, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(discovery.Aggregates.Select(a => a.DomainName), StringComparer.OrdinalIgnoreCase);

        IDomainQueryHandler[] materializedQueryHandlers = DomainQueryHandlerRouteValidator.MaterializeAndValidate(queryHandlers);

        ILookup<string, string> queryTypesByDomain = materializedQueryHandlers
            .ToLookup(h => h.Domain, h => h.QueryType, StringComparer.OrdinalIgnoreCase);

        List<DomainMetadata> domains = [.. discovery.Aggregates
            .Where(a => requested.Contains(a.DomainName))
            .Select(a => CreateDomainMetadata(a, discovery, queryTypesByDomain))
            .OrderBy(d => d.Domain, StringComparer.OrdinalIgnoreCase)];

        return new Response(domains);
    }

    private static DomainMetadata CreateDomainMetadata(
        DiscoveredDomain aggregate,
        DiscoveryResult discovery,
        ILookup<string, string> queryTypesByDomain) {
        Type aggregateType = aggregate.Type;
        Type stateType = aggregate.StateType;
        List<string> commandTypes = [.. aggregateType
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == "Handle" && IsDomainResultReturn(m.ReturnType))
            .Select(m => m.GetParameters().FirstOrDefault()?.ParameterType)
            .Where(t => t is not null)
            .Select(t => t!.FullName ?? t.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        List<string> eventTypes = [.. stateType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == "Apply" && m.GetParameters().Length == 1)
            .Select(m => m.GetParameters()[0].ParameterType)
            .Where(t => typeof(IEventPayload).IsAssignableFrom(t))
            .Select(t => t.FullName ?? t.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        List<string> rejectionEventTypes = [.. stateType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == "Apply" && m.GetParameters().Length == 1)
            .Select(m => m.GetParameters()[0].ParameterType)
            .Where(t => typeof(IRejectionEvent).IsAssignableFrom(t))
            .Select(t => t.FullName ?? t.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        List<string> projectionNames = [.. discovery.Projections
            .Where(p => p.DomainName.Equals(aggregate.DomainName, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.DomainName)
            .Concat(DiscoverProjectionHandlerNames(aggregateType.Assembly, aggregate.DomainName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)];

        List<string> queryTypes = [.. queryTypesByDomain[aggregate.DomainName]
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        return new DomainMetadata(
            aggregate.DomainName,
            eventTypes,
            rejectionEventTypes,
            commandTypes,
            [aggregateType.FullName ?? aggregateType.Name],
            projectionNames,
            queryTypes);
    }

    private static IEnumerable<string> DiscoverProjectionHandlerNames(Assembly assembly, string domain)
        => assembly.GetTypes()
            .Where(t => t.IsClass && t.IsAbstract && t.IsSealed && t.Name.EndsWith("ProjectionHandler", StringComparison.Ordinal))
            .Where(t => (t.Namespace ?? string.Empty).Contains($".{ToPascalCase(domain)}.", StringComparison.Ordinal))
            .Select(_ => domain);

    private static bool IsDomainResultReturn(Type returnType)
        => returnType == typeof(DomainResult)
            || returnType == typeof(Task<DomainResult>);

    private static string ToPascalCase(string value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : $"{char.ToUpperInvariant(value[0])}{value[1..]}";

    /// <summary>The operational-index metadata request: the domains to include.</summary>
    /// <param name="Domains">The requested domain names.</param>
    public sealed record Request(IReadOnlyList<string> Domains);

    /// <summary>The operational-index metadata response.</summary>
    /// <param name="Domains">The per-domain metadata entries.</param>
    public sealed record Response(IReadOnlyList<DomainMetadata> Domains);

    /// <summary>Operational metadata for a single domain.</summary>
    /// <param name="Domain">The kebab-case domain name.</param>
    /// <param name="EventTypes">Full names of the domain's event payload types.</param>
    /// <param name="RejectionEventTypes">Full names of the domain's rejection event types.</param>
    /// <param name="CommandTypes">Full names of the domain's command types.</param>
    /// <param name="AggregateTypes">Full names of the domain's aggregate types.</param>
    /// <param name="ProjectionNames">The domain's projection names.</param>
    /// <param name="QueryTypes">The query types the domain serves via <see cref="IDomainQueryHandler"/>.</param>
    public sealed record DomainMetadata(
        string Domain,
        IReadOnlyList<string> EventTypes,
        IReadOnlyList<string> RejectionEventTypes,
        IReadOnlyList<string> CommandTypes,
        IReadOnlyList<string> AggregateTypes,
        IReadOnlyList<string> ProjectionNames,
        IReadOnlyList<string> QueryTypes);
}
