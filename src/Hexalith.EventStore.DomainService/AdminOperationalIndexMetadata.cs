using System.Reflection;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Projections;
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
        IEnumerable<IDomainQueryHandler>? queryHandlers = null)
        => CreateCore(
            discovery,
            requestedDomains,
            queryHandlers,
            namedProjectionHandlers: null,
            appId: null,
            serviceVersion: null,
            options: null);

    /// <summary>
    /// Creates capability-bound operational metadata including exact named projection routes.
    /// </summary>
    /// <param name="discovery">The discovery result for the hosted domains.</param>
    /// <param name="requestedDomains">The domains to include, or <c>null</c>/empty for all.</param>
    /// <param name="queryHandlers">The registered query handlers.</param>
    /// <param name="namedProjectionHandlers">The registered named asynchronous projection handlers.</param>
    /// <param name="appId">The DAPR application identity to bind to this catalog.</param>
    /// <param name="serviceVersion">The domain-service version to bind to this catalog.</param>
    /// <param name="options">The named dispatch limits.</param>
    /// <returns>The operational-index metadata response.</returns>
    public static Response Create(
        DiscoveryResult discovery,
        IReadOnlyList<string>? requestedDomains,
        IEnumerable<IDomainQueryHandler>? queryHandlers,
        IEnumerable<IAsyncDomainProjectionHandler>? namedProjectionHandlers,
        string appId,
        string serviceVersion,
        ProjectionDispatchOptions options) {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceVersion);
        ArgumentNullException.ThrowIfNull(options);

        return CreateCore(
            discovery,
            requestedDomains,
            queryHandlers,
            namedProjectionHandlers,
            appId,
            serviceVersion,
            options);
    }

    private static Response CreateCore(
        DiscoveryResult discovery,
        IReadOnlyList<string>? requestedDomains,
        IEnumerable<IDomainQueryHandler>? queryHandlers,
        IEnumerable<IAsyncDomainProjectionHandler>? namedProjectionHandlers,
        string? appId,
        string? serviceVersion,
        ProjectionDispatchOptions? options) {
        ArgumentNullException.ThrowIfNull(discovery);

        HashSet<string> requested = requestedDomains is { Count: > 0 }
            ? new HashSet<string>(requestedDomains, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(discovery.Aggregates.Select(a => a.DomainName), StringComparer.OrdinalIgnoreCase);

        IDomainQueryHandler[] materializedQueryHandlers = DomainQueryHandlerRouteValidator.MaterializeAndValidate(queryHandlers);
        IAsyncDomainProjectionHandler[] materializedNamedHandlers = options is null
            ? []
            : DomainProjectionHandlerRouteValidator.MaterializeAndValidateNamed(namedProjectionHandlers, options);

        ILookup<string, string> queryTypesByDomain = materializedQueryHandlers
            .ToLookup(h => h.Domain, h => h.QueryType, StringComparer.OrdinalIgnoreCase);
        ILookup<string, string> namedProjectionTypesByDomain = materializedNamedHandlers
            .Where(handler => requested.Contains(handler.Domain))
            .ToLookup(handler => handler.Domain, handler => handler.ProjectionType, StringComparer.Ordinal);

        List<DomainMetadata> domains = [.. discovery.Aggregates
            .Where(a => requested.Contains(a.DomainName))
            .Select(a => CreateDomainMetadata(a, discovery, queryTypesByDomain, namedProjectionTypesByDomain))
            .OrderBy(d => d.Domain, StringComparer.OrdinalIgnoreCase)];

        if (materializedNamedHandlers.Length == 0 || appId is null || serviceVersion is null) {
            return new Response(domains);
        }

        ProjectionDispatchRoute[] admittedRoutes = [.. materializedNamedHandlers
            .Where(handler => requested.Contains(handler.Domain))
            .Select(handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))];
        return new Response(domains) {
            DispatchVersion = ProjectionDispatchProtocol.Version,
            DispatchCapability = ProjectionDispatchProtocol.Capability,
            AppId = appId,
            ServiceVersion = serviceVersion,
            CatalogFingerprint = ProjectionRouteCatalogFingerprint.Compute(appId, serviceVersion, admittedRoutes),
        };
    }

    private static DomainMetadata CreateDomainMetadata(
        DiscoveredDomain aggregate,
        DiscoveryResult discovery,
        ILookup<string, string> queryTypesByDomain,
        ILookup<string, string> namedProjectionTypesByDomain) {
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
            queryTypes) {
            NamedProjectionTypes = [.. namedProjectionTypesByDomain[aggregate.DomainName]
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)],
        };
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
    public sealed record Request(IReadOnlyList<string> Domains) {
        /// <summary>Gets the optional DAPR app id requested for capability binding.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AppId { get; init; }

        /// <summary>Gets the optional domain-service version requested for capability binding.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ServiceVersion { get; init; }
    }

    /// <summary>The operational-index metadata response.</summary>
    /// <param name="Domains">The per-domain metadata entries.</param>
    public sealed record Response(IReadOnlyList<DomainMetadata> Domains) {
        /// <summary>Gets the optional named dispatch protocol version.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? DispatchVersion { get; init; }

        /// <summary>Gets the optional named dispatch capability marker.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DispatchCapability { get; init; }

        /// <summary>Gets the DAPR app id bound to the named route catalog.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AppId { get; init; }

        /// <summary>Gets the domain-service version bound to the named route catalog.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ServiceVersion { get; init; }

        /// <summary>Gets the deterministic fingerprint of the bound named route catalog.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CatalogFingerprint { get; init; }
    }

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
        IReadOnlyList<string> QueryTypes) {
        /// <summary>Gets the exact canonical named asynchronous projection types for this domain.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<string>? NamedProjectionTypes { get; init; }
    }
}
