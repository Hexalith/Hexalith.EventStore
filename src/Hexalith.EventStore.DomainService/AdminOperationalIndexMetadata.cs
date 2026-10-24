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
            options: null,
            keyedProcessorDomains: null);

    /// <summary>Creates legacy metadata including domains confirmed by server-side keyed registration.</summary>
    /// <param name="discovery">The discovery result for the hosted domains.</param>
    /// <param name="requestedDomains">The requested domains.</param>
    /// <param name="queryHandlers">Registered query handlers.</param>
    /// <param name="keyedProcessorDomains">Domains confirmed by the host's keyed service registry.</param>
    /// <returns>Legacy metadata without named-dispatch capabilities.</returns>
    public static Response Create(
        DiscoveryResult discovery,
        IReadOnlyList<string>? requestedDomains,
        IEnumerable<IDomainQueryHandler>? queryHandlers,
        IReadOnlyCollection<string> keyedProcessorDomains)
        => CreateCore(discovery, requestedDomains, queryHandlers, null, null, null, null, keyedProcessorDomains);

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
    /// <param name="keyedProcessorDomains">Explicitly requested domains backed by a registered keyed processor.</param>
    /// <returns>The operational-index metadata response.</returns>
    public static Response Create(
        DiscoveryResult discovery,
        IReadOnlyList<string>? requestedDomains,
        IEnumerable<IDomainQueryHandler>? queryHandlers,
        IEnumerable<IAsyncDomainProjectionHandler>? namedProjectionHandlers,
        string appId,
        string serviceVersion,
        ProjectionDispatchOptions options,
        IReadOnlyCollection<string>? keyedProcessorDomains = null,
        IEnumerable<IDomainProjectionHandler>? legacyProjectionHandlers = null) {
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
            options,
            keyedProcessorDomains,
            legacyProjectionHandlers);
    }

    private static Response CreateCore(
        DiscoveryResult discovery,
        IReadOnlyList<string>? requestedDomains,
        IEnumerable<IDomainQueryHandler>? queryHandlers,
        IEnumerable<IAsyncDomainProjectionHandler>? namedProjectionHandlers,
        string? appId,
        string? serviceVersion,
        ProjectionDispatchOptions? options,
        IReadOnlyCollection<string>? keyedProcessorDomains,
        IEnumerable<IDomainProjectionHandler>? legacyProjectionHandlers = null) {
        ArgumentNullException.ThrowIfNull(discovery);

        HashSet<string> requested = requestedDomains is { Count: > 0 }
            ? new HashSet<string>(requestedDomains, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(discovery.Aggregates.Select(a => a.DomainName), StringComparer.OrdinalIgnoreCase);

        IDomainQueryHandler[] materializedQueryHandlers = DomainQueryHandlerRouteValidator.MaterializeAndValidate(queryHandlers);
        if (requestedDomains is not { Count: > 0 }) requested.UnionWith(materializedQueryHandlers.Select(static handler => handler.Domain));
        IAsyncDomainProjectionHandler[] materializedNamedHandlers = options is null
            ? []
            : DomainProjectionHandlerRouteValidator.MaterializeAndValidateNamed(namedProjectionHandlers, options);

        ILookup<string, string> queryTypesByDomain = materializedQueryHandlers
            .ToLookup(h => h.Domain, h => h.QueryType, StringComparer.OrdinalIgnoreCase);
        ILookup<string, string> namedProjectionTypesByDomain = materializedNamedHandlers
            .Where(handler => requested.Contains(handler.Domain))
            .ToLookup(handler => handler.Domain, handler => handler.ProjectionType, StringComparer.Ordinal);
        ILookup<string, string> fullReplayTypesByDomain = materializedNamedHandlers
            .OfType<IAsyncDomainProjectionRebuildHandler>()
            .Where(handler => requested.Contains(handler.Domain)
                && handler is not IAsyncDomainProjectionSingleEventHandler
                && handler.RebuildSemantics == DomainProjectionRebuildSemantics.FullReplay)
            .ToLookup(handler => handler.Domain, handler => handler.ProjectionType, StringComparer.Ordinal);
        ILookup<string, string> sharedEpochTypesByDomain = materializedNamedHandlers
            .OfType<IAsyncDomainSharedProjectionEpochHandler>()
            .Where(handler => requested.Contains(handler.Domain) && handler.EpochEnabled)
            .ToLookup(handler => handler.Domain, handler => handler.ProjectionType, StringComparer.Ordinal);
        ILookup<string, string> singleEventTypesByDomain = materializedNamedHandlers
            .OfType<IAsyncDomainProjectionSingleEventHandler>()
            .Where(handler => requested.Contains(handler.Domain))
            .ToLookup(handler => handler.Domain, handler => handler.ProjectionType, StringComparer.Ordinal);
        ILookup<string, string> mappedOnlyStreamedTypesByDomain = materializedNamedHandlers
            .OfType<IAsyncDomainProjectionRebuildHandler>()
            .Where(handler => handler is IAsyncDomainProjectionStreamedRebuildHandler
                && legacyProjectionHandlers is not null
                && !legacyProjectionHandlers.Any(legacy => string.Equals(legacy.Domain, handler.Domain, StringComparison.Ordinal))
                && requested.Contains(handler.Domain)
                && handler.RebuildSemantics == DomainProjectionRebuildSemantics.FullReplay
                && !discovery.Projections.Any(projection => string.Equals(projection.DomainName, handler.Domain, StringComparison.Ordinal))
                && !discovery.Aggregates.Any(aggregate => string.Equals(aggregate.DomainName, handler.Domain, StringComparison.Ordinal)
                    && DiscoverProjectionHandlerNames(aggregate.Type.Assembly, handler.Domain).Any()))
            .ToLookup(handler => handler.Domain, handler => handler.ProjectionType, StringComparer.Ordinal);
        ILookup<string, string> incrementalReplayTypesByDomain = materializedNamedHandlers
            .OfType<IAsyncDomainProjectionIncrementalReplayHandler>()
            .Where(handler => requested.Contains(handler.Domain)
                && handler.RebuildSemantics == DomainProjectionRebuildSemantics.FullReplay)
            .ToLookup(handler => handler.Domain, handler => handler.ProjectionType, StringComparer.Ordinal);

        List<DomainMetadata> domains = [.. discovery.Aggregates
            .Where(a => requested.Contains(a.DomainName))
            .Select(a => CreateDomainMetadata(a, discovery, queryTypesByDomain, namedProjectionTypesByDomain, fullReplayTypesByDomain, mappedOnlyStreamedTypesByDomain, incrementalReplayTypesByDomain, sharedEpochTypesByDomain, singleEventTypesByDomain))
            .OrderBy(d => d.Domain, StringComparer.OrdinalIgnoreCase)];

        // A domain may use a keyed processor in place of EventStoreAggregate<TState>.
        // Admit its named projections only when the host confirms that processor registration.
        // A handler alone remains an orphan and must not create an invokable route.
        if (appId is not null && serviceVersion is not null && keyedProcessorDomains is not null) {
            foreach (string domain in keyedProcessorDomains
                .Where(requested.Contains)
                .Where(domain => namedProjectionTypesByDomain.Contains(domain))
                .Where(domain => domains.All(existing => !string.Equals(existing.Domain, domain, StringComparison.Ordinal)))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)) {
                domains.Add(new DomainMetadata(domain, [], [], [], [], [], [.. queryTypesByDomain[domain]
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)]) {
                    NamedProjectionTypes = [.. namedProjectionTypesByDomain[domain]
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)],
                    FullReplayProjectionTypes = [.. fullReplayTypesByDomain[domain]
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)],
                    MappedOnlyStreamedProjectionTypes = [.. mappedOnlyStreamedTypesByDomain[domain]
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)],
                    IncrementalReplayProjectionTypes = [.. incrementalReplayTypesByDomain[domain]
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)],
                    SharedEpochProjectionTypes = [.. sharedEpochTypesByDomain[domain]
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)],
                    SingleEventProjectionTypes = [.. singleEventTypesByDomain[domain]
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)],
                });
            }
        }

        domains = [.. domains.Select(domain => domain with {
            OrdinaryProjectionTypes = [.. materializedNamedHandlers
                .Where(handler => handler.OrdinaryDeliveryEnabled && string.Equals(handler.Domain, domain.Domain, StringComparison.Ordinal))
                .Select(static handler => handler.ProjectionType).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
        })];

        // Handler-only domains have a valid /query route even without an SDK aggregate. Advertise only
        // their registered query types; this must not admit orphan named projections or invent aggregate metadata.
        foreach (string domain in materializedQueryHandlers.Select(static handler => handler.Domain)
            .Where(requested.Contains).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal)) {
            if (domains.All(item => !string.Equals(item.Domain, domain, StringComparison.OrdinalIgnoreCase)))
                domains.Add(new DomainMetadata(domain, [], [], [], [], [],
                    [.. queryTypesByDomain[domain].Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]));
        }
        // Legacy callers still need to discover keyed-processor domains. Registration is
        // server-confirmed; neither a requested name nor an orphan projection is sufficient.
        // Add these after named capability shaping so legacy responses retain their old shape.
        if (appId is null && keyedProcessorDomains is not null) {
            foreach (string domain in keyedProcessorDomains.Where(static domain => !string.IsNullOrWhiteSpace(domain))
                .Where(requested.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal)) {
                if (domains.All(item => !string.Equals(item.Domain, domain, StringComparison.OrdinalIgnoreCase)))
                    domains.Add(new DomainMetadata(domain, [], [], [], [], [], []));
            }
        }
        domains = [.. domains.OrderBy(static domain => domain.Domain, StringComparer.OrdinalIgnoreCase)];

        if (materializedNamedHandlers.Length == 0 || appId is null || serviceVersion is null) {
            return new Response(domains);
        }

        // Compute the fingerprint over exactly the routes emitted in the per-domain metadata.
        // Unregistered orphan handlers are omitted, so EventStore's recomputed fingerprint agrees.
        ProjectionDispatchRoute[] admittedRoutes = [.. domains
            .Where(domain => domain.NamedProjectionTypes is { Count: > 0 })
            .SelectMany(domain => domain.NamedProjectionTypes!
                .Select(projectionType => new ProjectionDispatchRoute(domain.Domain, projectionType)))];
        if (admittedRoutes.Length == 0) {
            // Named handlers exist but none resolve to an emitted route. Do not
            // advertise a v2 capability with zero servable routes; present as a non-v2 service.
            return new Response(domains);
        }

        return new Response(domains) {
            DispatchVersion = ProjectionDispatchProtocol.Version,
            DispatchCapability = ProjectionDispatchProtocol.Capability,
            AppId = appId,
            ServiceVersion = serviceVersion,
            CatalogFingerprint = ProjectionRouteCatalogFingerprint.Compute(appId, serviceVersion, admittedRoutes,
                domains.SelectMany(domain => (domain.OrdinaryProjectionTypes ?? [])
                    .Select(type => new ProjectionDispatchRoute(domain.Domain, type)))),
        };
    }

    private static DomainMetadata CreateDomainMetadata(
        DiscoveredDomain aggregate,
        DiscoveryResult discovery,
        ILookup<string, string> queryTypesByDomain,
        ILookup<string, string> namedProjectionTypesByDomain,
        ILookup<string, string> fullReplayTypesByDomain,
        ILookup<string, string> mappedOnlyStreamedTypesByDomain,
        ILookup<string, string> incrementalReplayTypesByDomain,
        ILookup<string, string> sharedEpochTypesByDomain,
        ILookup<string, string> singleEventTypesByDomain) {
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
            FullReplayProjectionTypes = [.. fullReplayTypesByDomain[aggregate.DomainName]
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)],
            MappedOnlyStreamedProjectionTypes = [.. mappedOnlyStreamedTypesByDomain[aggregate.DomainName]
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)],
            IncrementalReplayProjectionTypes = [.. incrementalReplayTypesByDomain[aggregate.DomainName]
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)],
            SharedEpochProjectionTypes = [.. sharedEpochTypesByDomain[aggregate.DomainName]
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)],
            SingleEventProjectionTypes = [.. singleEventTypesByDomain[aggregate.DomainName]
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

        /// <summary>Gets the explicitly requested source-guard catalog version; absent preserves historical catalog bytes.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? ProjectionSourceGuardVersion { get; init; }
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
        /// <summary>Gets explicitly enrolled read-only source-guard proof descriptors by exact named route.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyDictionary<string, ProjectionSourceGuardCapability>? SourceGuardCapabilities { get; init; }

        /// <summary>Gets optional opaque host recovery bindings tied to the unchanged catalog identity.</summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyDictionary<string, ProjectionReplayRecoveryBinding>? ReplayRecoveryBindings { get; init; }
        /// <summary>Gets the exact canonical named asynchronous projection types for this domain.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<string>? NamedProjectionTypes { get; init; }

        /// <summary>Gets named routes that accept ordinary delivery; an empty set keeps rebuild-only routes available.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<string>? OrdinaryProjectionTypes { get; init; }

        /// <summary>Gets named routes that support aggregate-owned full replay for one owner.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<string>? FullReplayProjectionTypes { get; init; }

        /// <summary>Gets streamed full-replay routes whose domain has no legacy projection handler.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<string>? MappedOnlyStreamedProjectionTypes { get; init; }

        /// <summary>Gets full-replay routes that support contiguous ordinary suffix delivery.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<string>? IncrementalReplayProjectionTypes { get; init; }

        /// <summary>Gets routes whose normal delivery uses the fenced shared epoch journal.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<string>? SharedEpochProjectionTypes { get; init; }

        /// <summary>Gets named routes whose normal handler safely accepts repeated individual source events.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<string>? SingleEventProjectionTypes { get; init; }
    }
}
