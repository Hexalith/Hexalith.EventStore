using System.Collections.Concurrent;
using System.Net;
using System.Text.Json.Serialization;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Projections;
using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;
using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Indexes;

/// <summary>
/// Populates EventStore-owned derived admin indexes from startup discovery metadata.
/// </summary>
public sealed partial class AdminOperationalIndexHostedService(
    DaprClient daprClient,
    IHttpClientFactory httpClientFactory,
    IOptions<CommandStatusOptions> commandStatusOptions,
    IOptions<DomainServiceOptions> domainServiceOptions,
    IOptions<ProjectionOptions> projectionOptions,
    INamedProjectionRouteCatalog namedProjectionRouteCatalog,
    ILogger<AdminOperationalIndexHostedService> logger,
    IOptions<ProjectionDispatchOptions>? projectionDispatchOptions = null) : IHostedService, INamedProjectionCatalogRefresher {
    private const string MetadataMethodName = "admin/operational-index-metadata";
    private readonly string _stateStoreName = commandStatusOptions.Value.StateStoreName;
    private readonly ConcurrentDictionary<string, DomainServiceRegistration> _knownRegistrations = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _refreshStopping = new();
    private Task? _refreshTask;

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken) {
        foreach (DomainServiceRegistration registration in domainServiceOptions.Value.Registrations.Values) {
            TrackRegistration(registration);
        }
        _refreshTask = RefreshLoopAsync(_refreshStopping.Token);

        AdminOperationalIndexMetadataLoadResult metadataLoad = await LoadDomainMetadataAsync(cancellationToken).ConfigureAwait(false);
        if (metadataLoad.HasFailures) {
            Log.MetadataWriteSkipped(logger);
            return;
        }

        AdminOperationalIndexSnapshot snapshot = BuildSnapshot(
            metadataLoad.Metadata,
            domainServiceOptions.Value.Registrations.Values,
            projectionOptions.Value,
            metadataLoad.BoundMetadata);
        await WriteProjectionIndexAsync(snapshot, cancellationToken).ConfigureAwait(false);
        await WriteTypeCatalogIndexesAsync(snapshot, cancellationToken).ConfigureAwait(false);
        await WriteQueryTypeIndexAsync(snapshot, cancellationToken).ConfigureAwait(false);
        namedProjectionRouteCatalog.Replace(metadataLoad.NamedProjectionRoutes);
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken) {
        await _refreshStopping.CancelAsync().ConfigureAwait(false);
        if (_refreshTask is not null) {
            try {
                await _refreshTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_refreshStopping.IsCancellationRequested) {
            }
        }
    }

    /// <inheritdoc/>
    public async Task<bool> RefreshAsync(
        DomainServiceRegistration registration,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(registration);
        TrackRegistration(registration);
        (bool success, _, IReadOnlyList<NamedProjectionRouteCatalogEntry> entries) = await LoadBindingAsync(
            registration,
            cancellationToken).ConfigureAwait(false);
        if (!success) {
            throw new InvalidOperationException(
                $"Named projection metadata refresh failed for '{registration.AppId}/{GetServiceVersion(registration)}/{registration.Domain}'.");
        }

        string version = GetServiceVersion(registration);
        if (entries.Count == 0) {
            namedProjectionRouteCatalog.Remove(registration.AppId, version, registration.Domain);
            return false;
        }

        namedProjectionRouteCatalog.Upsert(entries);
        return true;
    }

    private async Task<AdminOperationalIndexMetadataLoadResult> LoadDomainMetadataAsync(CancellationToken ct) {
        DomainServiceRegistration[] bindings = [.. domainServiceOptions.Value.Registrations.Values
            .Where(static registration => !string.IsNullOrWhiteSpace(registration.AppId)
                && !string.IsNullOrWhiteSpace(registration.Domain))
            .GroupBy(static registration => $"{registration.AppId}\u001f{GetServiceVersion(registration)}\u001f{registration.Domain}", StringComparer.Ordinal)
            .Select(static group => group.First())];

        var results = new List<AdminOperationalIndexDomainMetadata>();
        var boundMetadata = new Dictionary<(string AppId, string ServiceVersion, string Domain), AdminOperationalIndexDomainMetadata>();
        var namedProjectionEntries = new List<NamedProjectionRouteCatalogEntry>();
        bool hasFailures = false;
        foreach (DomainServiceRegistration registration in bindings) {
            try {
                (bool success, AdminOperationalIndexDomainMetadata? bindingMetadata, IReadOnlyList<NamedProjectionRouteCatalogEntry> entries) =
                    await LoadBindingAsync(registration, ct).ConfigureAwait(false);
                if (!success) {
                    hasFailures = true;
                    continue;
                }

                if (bindingMetadata is not null) {
                    results.Add(bindingMetadata);
                    boundMetadata[(registration.AppId, GetServiceVersion(registration), registration.Domain)] = bindingMetadata;
                }

                namedProjectionEntries.AddRange(entries);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) {
                hasFailures = true;
                Log.MetadataUnavailable(logger, registration.AppId, ex.GetType().Name);
            }
        }

        IReadOnlyList<AdminOperationalIndexDomainMetadata> metadata = [.. results
            .GroupBy(d => d.Domain, StringComparer.OrdinalIgnoreCase)
            .Select(g => MergeDomainMetadata(g.Key, g))
            .OrderBy(d => d.Domain, StringComparer.OrdinalIgnoreCase)];
        NamedProjectionRouteCatalogSnapshot namedRoutes;
        try {
            namedRoutes = new NamedProjectionRouteCatalogSnapshot(namedProjectionEntries);
        }
        catch (InvalidOperationException) {
            hasFailures = true;
            namedRoutes = NamedProjectionRouteCatalogSnapshot.Empty;
        }

        return new AdminOperationalIndexMetadataLoadResult(metadata, namedRoutes, hasFailures, boundMetadata);
    }

    private async Task<(bool Success, AdminOperationalIndexDomainMetadata? Metadata, IReadOnlyList<NamedProjectionRouteCatalogEntry> Entries)>
        LoadBindingAsync(DomainServiceRegistration registration, CancellationToken cancellationToken) {
        string serviceVersion = GetServiceVersion(registration);
        var request = new AdminOperationalIndexMetadataRequest([registration.Domain]) {
            AppId = registration.AppId,
            ServiceVersion = serviceVersion,
        };
        using HttpRequestMessage httpRequest = daprClient.CreateInvokeMethodRequest(
            registration.AppId,
            MetadataMethodName,
            request);
        HttpClient httpClient = httpClientFactory.CreateClient();
        using HttpResponseMessage response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) {
            Log.MetadataEndpointAbsent(logger, registration.AppId);
            return (true, null, []);
        }

        _ = response.EnsureSuccessStatusCode();
        AdminOperationalIndexMetadataResponse? responseMetadata = await response.Content
            .ReadFromJsonAsync<AdminOperationalIndexMetadataResponse>(cancellationToken)
            .ConfigureAwait(false);
        if (responseMetadata is null) {
            return (false, null, []);
        }

        AdminOperationalIndexDomainMetadata? metadata = responseMetadata.Domains.SingleOrDefault(
            domain => string.Equals(domain.Domain, registration.Domain, StringComparison.Ordinal));
        if (TryCreateNamedProjectionEntries(
            responseMetadata,
            registration.AppId,
            serviceVersion,
            projectionDispatchOptions?.Value ?? new ProjectionDispatchOptions(),
            out IReadOnlyList<NamedProjectionRouteCatalogEntry> entries)) {
            return (metadata is not null, metadata, entries);
        }

        if (HasNamedProjectionMetadata(responseMetadata)) {
            Log.NamedProjectionMetadataRejected(logger, registration.AppId, serviceVersion);
            return (false, metadata, []);
        }

        return (metadata is not null, metadata, []);
    }

    internal static bool TryCreateNamedProjectionEntries(
        AdminOperationalIndexMetadataResponse response,
        string expectedAppId,
        string expectedServiceVersion,
        out IReadOnlyList<NamedProjectionRouteCatalogEntry> entries) {
        return TryCreateNamedProjectionEntries(
            response,
            expectedAppId,
            expectedServiceVersion,
            new ProjectionDispatchOptions(),
            out entries);
    }

    internal static bool TryCreateNamedProjectionEntries(
        AdminOperationalIndexMetadataResponse response,
        string expectedAppId,
        string expectedServiceVersion,
        ProjectionDispatchOptions dispatchOptions,
        out IReadOnlyList<NamedProjectionRouteCatalogEntry> entries) {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedAppId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedServiceVersion);
        ArgumentNullException.ThrowIfNull(dispatchOptions);
        dispatchOptions.Validate();

        entries = [];
        if (response.DispatchVersion != ProjectionDispatchProtocol.Version
            || !string.Equals(response.DispatchCapability, ProjectionDispatchProtocol.Capability, StringComparison.Ordinal)
            || !string.Equals(response.AppId, expectedAppId, StringComparison.Ordinal)
            || !string.Equals(response.ServiceVersion, expectedServiceVersion, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(response.CatalogFingerprint)) {
            return false;
        }

        try {
            ProjectionDispatchRoute[] routes = [.. response.Domains
                .Where(static domain => domain.NamedProjectionTypes is { Count: > 0 })
                .SelectMany(domain => domain.NamedProjectionTypes!
                    .Select(projectionType => new ProjectionDispatchRoute(domain.Domain, projectionType)))];
            if (routes.Length == 0) {
                return false;
            }

            if (routes.Length > dispatchOptions.MaxOutcomes
                || routes.GroupBy(static route => route.Domain, StringComparer.Ordinal)
                    .Any(group => group.Count() > dispatchOptions.MaxHandlersPerDomain)) {
                return false;
            }

            foreach (ProjectionDispatchRoute route in routes) {
                NamingConventionEngine.ValidateKebabCase(route.Domain, nameof(route.Domain));
                NamingConventionEngine.ValidateKebabCase(route.ProjectionType, nameof(route.ProjectionType));
            }

            if (routes.Distinct().Count() != routes.Length) {
                return false;
            }

            string expectedFingerprint = ProjectionRouteCatalogFingerprint.Compute(
                expectedAppId,
                expectedServiceVersion,
                routes);
            if (!string.Equals(response.CatalogFingerprint, expectedFingerprint, StringComparison.Ordinal)) {
                return false;
            }

            entries = [.. routes
                .GroupBy(static route => route.Domain, StringComparer.Ordinal)
                .OrderBy(static group => group.Key, StringComparer.Ordinal)
                .Select(group => new NamedProjectionRouteCatalogEntry(
                    expectedAppId,
                    expectedServiceVersion,
                    group.Key,
                    ProjectionDispatchProtocol.Version,
                    ProjectionDispatchProtocol.Capability,
                    expectedFingerprint,
                    group.Select(static route => route.ProjectionType).Order(StringComparer.Ordinal)))];
            return true;
        }
        catch (ArgumentException) {
            entries = [];
            return false;
        }
    }

    private static bool HasNamedProjectionMetadata(AdminOperationalIndexMetadataResponse response)
        => response.DispatchVersion is not null
            || response.DispatchCapability is not null
            || response.AppId is not null
            || response.ServiceVersion is not null
            || response.CatalogFingerprint is not null
            || response.Domains.Any(static domain => domain.NamedProjectionTypes is { Count: > 0 });

    public static AdminOperationalIndexSnapshot BuildSnapshot(
        IReadOnlyList<AdminOperationalIndexDomainMetadata> metadata,
        IEnumerable<DomainServiceRegistration> registrations,
        ProjectionOptions projectionOptions,
        IReadOnlyDictionary<(string AppId, string ServiceVersion, string Domain), AdminOperationalIndexDomainMetadata>? boundMetadata = null) {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(projectionOptions);

        List<DomainServiceRegistration> materializedRegistrations = [.. registrations];
        List<string> knownTenants = [.. materializedRegistrations
            .Select(r => r.TenantId)
            .Where(static t => !string.IsNullOrWhiteSpace(t) && !string.Equals(t, "*", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)];
        var metadataByDomain = metadata
            .GroupBy(static item => item.Domain, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => MergeDomainMetadata(group.Key, group),
                StringComparer.OrdinalIgnoreCase);
        var all = new List<ProjectionStatus>();
        var byTenant = new Dictionary<string, List<ProjectionStatus>>(StringComparer.OrdinalIgnoreCase);

        foreach (DomainServiceRegistration registration in materializedRegistrations) {
            if (string.IsNullOrWhiteSpace(registration.Domain)) {
                continue;
            }

            string serviceVersion = GetServiceVersion(registration);
            AdminOperationalIndexDomainMetadata? domainMetadata = boundMetadata is not null
                && boundMetadata.TryGetValue((registration.AppId, serviceVersion, registration.Domain), out AdminOperationalIndexDomainMetadata? bound)
                ? bound
                : boundMetadata is null && metadataByDomain.TryGetValue(registration.Domain, out AdminOperationalIndexDomainMetadata? unbound)
                    ? unbound
                    : null;
            IReadOnlyList<string> projectionNames = domainMetadata is not null
                ? [.. domainMetadata.ProjectionNames
                    .Concat(domainMetadata.NamedProjectionTypes ?? [])
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)]
                : [];

            foreach (string projectionName in projectionNames.Where(static p => !IsProjectionActorStateKey(p))) {
                ProjectionStatus projection = new(
                    projectionName,
                    string.Equals(registration.TenantId, "*", StringComparison.Ordinal) ? "all" : registration.TenantId,
                    ProjectionStatusType.Running,
                    Lag: projectionOptions.GetRefreshIntervalMs(registration.Domain) == 0 ? 0 : 1,
                    Throughput: 0,
                    ErrorCount: 0,
                    LastProcessedPosition: 0,
                    LastProcessedUtc: DateTimeOffset.UnixEpoch);

                all.Add(projection);
                IReadOnlyList<string> targetTenants = string.Equals(registration.TenantId, "*", StringComparison.Ordinal)
                    ? knownTenants
                    : [registration.TenantId];

                foreach (string targetTenant in targetTenants) {
                    if (!byTenant.TryGetValue(targetTenant, out List<ProjectionStatus>? scoped)) {
                        scoped = [];
                        byTenant[targetTenant] = scoped;
                    }

                    scoped.Add(CopyProjectionForTenant(projection, targetTenant));
                }
            }
        }

        all = [.. all
            .GroupBy(p => $"{p.TenantId}\u001f{p.Name}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(p => p.TenantId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)];

        List<EventTypeInfo> events = [.. metadata
            .SelectMany(m => m.EventTypes.Select(e => new EventTypeInfo(e, m.Domain, m.RejectionEventTypes.Contains(e, StringComparer.Ordinal), SchemaVersion: 1)))
            .OrderBy(e => e.Domain, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.TypeName, StringComparer.Ordinal)];
        List<CommandTypeInfo> commands = [.. metadata
            .SelectMany(m => m.CommandTypes.Select(c => new CommandTypeInfo(c, m.Domain, m.AggregateTypes.FirstOrDefault() ?? $"{ToPascalCase(m.Domain)}Aggregate")))
            .OrderBy(c => c.Domain, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.TypeName, StringComparer.Ordinal)];
        List<AggregateTypeInfo> aggregates = [.. metadata
            .SelectMany(m => m.AggregateTypes.Select(a => new AggregateTypeInfo(
                a,
                m.Domain,
                m.EventTypes.Count,
                m.CommandTypes.Count,
                m.ProjectionNames.Count > 0 || m.NamedProjectionTypes is { Count: > 0 })))
            .OrderBy(a => a.Domain, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.TypeName, StringComparer.Ordinal)];

        var normalizedByTenant = byTenant.ToDictionary(
            p => p.Key,
            p => p.Value
                .GroupBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            StringComparer.OrdinalIgnoreCase);

        var queryTypesByDomain = metadata
            .GroupBy(static item => item.Domain, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<string>)[.. group
                    .SelectMany(static item => item.QueryTypes ?? [])
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)],
                StringComparer.OrdinalIgnoreCase);

        return new AdminOperationalIndexSnapshot(all, normalizedByTenant, events, commands, aggregates, queryTypesByDomain);
    }

    private async Task RefreshLoopAsync(CancellationToken cancellationToken) {
        ProjectionDispatchOptions options = projectionDispatchOptions?.Value ?? new ProjectionDispatchOptions();
        options.Validate();
        using var timer = new PeriodicTimer(options.CatalogRefreshInterval);
        try {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false)) {
                foreach (DomainServiceRegistration registration in _knownRegistrations.Values) {
                    cancellationToken.ThrowIfCancellationRequested();
                    try {
                        _ = await RefreshAsync(registration, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                        throw;
                    }
                    catch (Exception ex) {
                        Log.MetadataUnavailable(logger, registration.AppId, ex.GetType().Name);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
        }
    }

    private void TrackRegistration(DomainServiceRegistration registration) {
        if (string.IsNullOrWhiteSpace(registration.AppId) || string.IsNullOrWhiteSpace(registration.Domain)) {
            return;
        }

        _knownRegistrations[$"{registration.AppId}\u001f{GetServiceVersion(registration)}\u001f{registration.Domain}"] = registration;
    }

    private static string GetServiceVersion(DomainServiceRegistration registration)
        => string.IsNullOrWhiteSpace(registration.Version) ? "v1" : registration.Version;

    private async Task WriteProjectionIndexAsync(AdminOperationalIndexSnapshot snapshot, CancellationToken ct) {
        await daprClient.SaveStateAsync(_stateStoreName, "admin:projections:all", snapshot.Projections, cancellationToken: ct).ConfigureAwait(false);
        foreach ((string tenantId, List<ProjectionStatus> scoped) in snapshot.TenantProjections) {
            await daprClient.SaveStateAsync(
                _stateStoreName,
                $"admin:projections:{tenantId}",
                scoped.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                cancellationToken: ct).ConfigureAwait(false);
        }
    }

    private async Task WriteTypeCatalogIndexesAsync(AdminOperationalIndexSnapshot snapshot, CancellationToken ct) {
        await SaveCatalogScopeAsync("all", snapshot.EventTypes, snapshot.CommandTypes, snapshot.AggregateTypes, ct).ConfigureAwait(false);
        foreach (string domain in snapshot.EventTypes.Select(m => m.Domain)
            .Concat(snapshot.CommandTypes.Select(c => c.Domain))
            .Concat(snapshot.AggregateTypes.Select(a => a.Domain))
            .Distinct(StringComparer.OrdinalIgnoreCase)) {
            await SaveCatalogScopeAsync(
                domain,
                snapshot.EventTypes.Where(e => e.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase)).ToList(),
                snapshot.CommandTypes.Where(c => c.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase)).ToList(),
                snapshot.AggregateTypes.Where(a => a.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase)).ToList(),
                ct).ConfigureAwait(false);
        }
    }

    private async Task WriteQueryTypeIndexAsync(AdminOperationalIndexSnapshot snapshot, CancellationToken ct) {
        if (snapshot.QueryTypesByDomain is null) {
            return;
        }

        foreach ((string domain, IReadOnlyList<string> queryTypes) in snapshot.QueryTypesByDomain) {
            await daprClient.SaveStateAsync(
                _stateStoreName,
                $"admin:query-types:{domain}",
                queryTypes.ToList(),
                cancellationToken: ct).ConfigureAwait(false);
        }
    }

    private async Task SaveCatalogScopeAsync(
        string scope,
        IReadOnlyList<EventTypeInfo> events,
        IReadOnlyList<CommandTypeInfo> commands,
        IReadOnlyList<AggregateTypeInfo> aggregates,
        CancellationToken ct) {
        await daprClient.SaveStateAsync(_stateStoreName, $"admin:type-catalog:events:{scope}", events, cancellationToken: ct).ConfigureAwait(false);
        await daprClient.SaveStateAsync(_stateStoreName, $"admin:type-catalog:commands:{scope}", commands, cancellationToken: ct).ConfigureAwait(false);
        await daprClient.SaveStateAsync(_stateStoreName, $"admin:type-catalog:aggregates:{scope}", aggregates, cancellationToken: ct).ConfigureAwait(false);
    }

    internal static bool IsProjectionActorStateKey(string value)
        => value.Contains("ProjectionActor", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith("projection-state", StringComparison.OrdinalIgnoreCase);

    private static ProjectionStatus CopyProjectionForTenant(ProjectionStatus projection, string tenantId)
        => new(
            projection.Name,
            tenantId,
            projection.Status,
            projection.Lag,
            projection.Throughput,
            projection.ErrorCount,
            projection.LastProcessedPosition,
            projection.LastProcessedUtc);

    private static AdminOperationalIndexDomainMetadata MergeDomainMetadata(
        string domain,
        IEnumerable<AdminOperationalIndexDomainMetadata> items)
        => new(
            domain,
            [.. items.SelectMany(i => i.EventTypes).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            [.. items.SelectMany(i => i.RejectionEventTypes).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            [.. items.SelectMany(i => i.CommandTypes).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            [.. items.SelectMany(i => i.AggregateTypes).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            [.. items.SelectMany(i => i.ProjectionNames).Where(static p => !IsProjectionActorStateKey(p)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            [.. items.SelectMany(i => i.QueryTypes ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]) {
            NamedProjectionTypes = [.. items
                .SelectMany(static item => item.NamedProjectionTypes ?? [])
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)],
        };

    private static string ToPascalCase(string value)
        => string.IsNullOrWhiteSpace(value)
            ? "Unknown"
            : $"{char.ToUpperInvariant(value[0])}{value[1..]}";

    private static partial class Log {
        [LoggerMessage(
            EventId = 6100,
            Level = LogLevel.Warning,
            Message = "Operational index metadata unavailable from domain service AppId={AppId}; type catalog entries for that app may be incomplete. ExceptionType={ExceptionType}")]
        public static partial void MetadataUnavailable(ILogger logger, string appId, string exceptionType);

        [LoggerMessage(
            EventId = 6101,
            Level = LogLevel.Warning,
            Message = "Skipping admin operational index writes because one or more domain metadata sources failed. Existing indexes are preserved.")]
        public static partial void MetadataWriteSkipped(ILogger logger);

        [LoggerMessage(
            EventId = 6102,
            Level = LogLevel.Information,
            Message = "Domain service AppId={AppId} does not expose the operational-index metadata endpoint (404); it is treated as a consumer and skipped without failing the index build.")]
        public static partial void MetadataEndpointAbsent(ILogger logger, string appId);

        [LoggerMessage(
            EventId = 6103,
            Level = LogLevel.Warning,
            Message = "Named projection metadata rejected for AppId={AppId}, ServiceVersion={ServiceVersion}; the previous complete route catalog is preserved.")]
        public static partial void NamedProjectionMetadataRejected(ILogger logger, string appId, string serviceVersion);
    }
}

internal sealed record AdminOperationalIndexMetadataLoadResult(
    IReadOnlyList<AdminOperationalIndexDomainMetadata> Metadata,
    NamedProjectionRouteCatalogSnapshot NamedProjectionRoutes,
    bool HasFailures,
    IReadOnlyDictionary<(string AppId, string ServiceVersion, string Domain), AdminOperationalIndexDomainMetadata> BoundMetadata);

/// <summary>Metadata request sent from EventStore to domain services for admin index population.</summary>
/// <param name="Domains">Configured domains for the target domain service.</param>
public sealed record AdminOperationalIndexMetadataRequest(IReadOnlyList<string> Domains) {
    /// <summary>Gets the DAPR app id requested for capability binding.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AppId { get; init; }

    /// <summary>Gets the domain-service version requested for capability binding.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ServiceVersion { get; init; }
}

/// <summary>Domain-service metadata response used to populate derived admin indexes.</summary>
/// <param name="Domains">Domain metadata entries.</param>
public sealed record AdminOperationalIndexMetadataResponse(IReadOnlyList<AdminOperationalIndexDomainMetadata> Domains) {
    /// <summary>Gets the optional named dispatch protocol version.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DispatchVersion { get; init; }

    /// <summary>Gets the optional named dispatch capability marker.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DispatchCapability { get; init; }

    /// <summary>Gets the DAPR app id bound to the route catalog.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AppId { get; init; }

    /// <summary>Gets the domain-service version bound to the route catalog.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ServiceVersion { get; init; }

    /// <summary>Gets the deterministic verified catalog fingerprint.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CatalogFingerprint { get; init; }
}

/// <summary>Metadata for one EventStore domain.</summary>
public sealed record AdminOperationalIndexDomainMetadata(
    string Domain,
    IReadOnlyList<string> EventTypes,
    IReadOnlyList<string> RejectionEventTypes,
    IReadOnlyList<string> CommandTypes,
    IReadOnlyList<string> AggregateTypes,
    IReadOnlyList<string> ProjectionNames,
    IReadOnlyList<string>? QueryTypes = null) {
    /// <summary>Gets the exact canonical named asynchronous projection types for this domain.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? NamedProjectionTypes { get; init; }
}

/// <summary>Materialized admin operational index payloads ready for state-store writes.</summary>
public sealed record AdminOperationalIndexSnapshot(
    IReadOnlyList<ProjectionStatus> Projections,
    IReadOnlyDictionary<string, List<ProjectionStatus>> TenantProjections,
    IReadOnlyList<EventTypeInfo> EventTypes,
    IReadOnlyList<CommandTypeInfo> CommandTypes,
    IReadOnlyList<AggregateTypeInfo> AggregateTypes,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? QueryTypesByDomain = null);
