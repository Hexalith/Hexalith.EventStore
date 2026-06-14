using System.Net;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Projections;
using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;

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
    ILogger<AdminOperationalIndexHostedService> logger) : IHostedService {
    private const string MetadataMethodName = "admin/operational-index-metadata";
    private readonly string _stateStoreName = commandStatusOptions.Value.StateStoreName;

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken) {
        AdminOperationalIndexMetadataLoadResult metadataLoad = await LoadDomainMetadataAsync(cancellationToken).ConfigureAwait(false);
        if (metadataLoad.HasFailures) {
            Log.MetadataWriteSkipped(logger);
            return;
        }

        AdminOperationalIndexSnapshot snapshot = BuildSnapshot(
            metadataLoad.Metadata,
            domainServiceOptions.Value.Registrations.Values,
            projectionOptions.Value);
        await WriteProjectionIndexAsync(snapshot, cancellationToken).ConfigureAwait(false);
        await WriteTypeCatalogIndexesAsync(snapshot, cancellationToken).ConfigureAwait(false);
        await WriteQueryTypeIndexAsync(snapshot, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<AdminOperationalIndexMetadataLoadResult> LoadDomainMetadataAsync(CancellationToken ct) {
        Dictionary<string, HashSet<string>> domainsByAppId = new(StringComparer.OrdinalIgnoreCase);
        foreach (DomainServiceRegistration registration in domainServiceOptions.Value.Registrations.Values) {
            if (string.IsNullOrWhiteSpace(registration.AppId) || string.IsNullOrWhiteSpace(registration.Domain)) {
                continue;
            }

            if (!domainsByAppId.TryGetValue(registration.AppId, out HashSet<string>? domains)) {
                domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                domainsByAppId[registration.AppId] = domains;
            }

            _ = domains.Add(registration.Domain);
        }

        var results = new List<AdminOperationalIndexDomainMetadata>();
        bool hasFailures = false;
        foreach ((string appId, HashSet<string> domains) in domainsByAppId) {
            try {
                var request = new AdminOperationalIndexMetadataRequest([.. domains.Order(StringComparer.OrdinalIgnoreCase)]);
                using HttpRequestMessage httpRequest = daprClient.CreateInvokeMethodRequest(appId, MetadataMethodName, request);
                HttpClient httpClient = httpClientFactory.CreateClient();
                using HttpResponseMessage response = await httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);

                // A 404 means the target app does not expose /admin/operational-index-metadata: it is a
                // pub/sub consumer, not a metadata-bearing domain service. Skip it WITHOUT marking a failure
                // so the indexes for domains that DID respond are still written — a single endpoint-less
                // consumer must not suppress the entire operational index.
                if (response.StatusCode == HttpStatusCode.NotFound) {
                    Log.MetadataEndpointAbsent(logger, appId);
                    continue;
                }

                _ = response.EnsureSuccessStatusCode();

                AdminOperationalIndexMetadataResponse? responseMetadata = await response.Content
                    .ReadFromJsonAsync<AdminOperationalIndexMetadataResponse>(ct)
                    .ConfigureAwait(false);
                if (responseMetadata is not null) {
                    results.AddRange(responseMetadata.Domains);
                }
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) {
                hasFailures = true;
                Log.MetadataUnavailable(logger, appId, ex.GetType().Name);
            }
        }

        IReadOnlyList<AdminOperationalIndexDomainMetadata> metadata = [.. results
            .GroupBy(d => d.Domain, StringComparer.OrdinalIgnoreCase)
            .Select(g => MergeDomainMetadata(g.Key, g))
            .OrderBy(d => d.Domain, StringComparer.OrdinalIgnoreCase)];
        return new AdminOperationalIndexMetadataLoadResult(metadata, hasFailures);
    }

    public static AdminOperationalIndexSnapshot BuildSnapshot(
        IReadOnlyList<AdminOperationalIndexDomainMetadata> metadata,
        IEnumerable<DomainServiceRegistration> registrations,
        ProjectionOptions projectionOptions) {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(projectionOptions);

        List<DomainServiceRegistration> materializedRegistrations = [.. registrations];
        List<string> knownTenants = [.. materializedRegistrations
            .Select(r => r.TenantId)
            .Where(static t => !string.IsNullOrWhiteSpace(t) && !string.Equals(t, "*", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)];
        var metadataByDomain = metadata.ToDictionary(m => m.Domain, StringComparer.OrdinalIgnoreCase);
        var all = new List<ProjectionStatus>();
        var byTenant = new Dictionary<string, List<ProjectionStatus>>(StringComparer.OrdinalIgnoreCase);

        foreach (DomainServiceRegistration registration in materializedRegistrations) {
            if (string.IsNullOrWhiteSpace(registration.Domain)) {
                continue;
            }

            IReadOnlyList<string> projectionNames = metadataByDomain.TryGetValue(registration.Domain, out AdminOperationalIndexDomainMetadata? domainMetadata)
                ? domainMetadata.ProjectionNames
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
            .SelectMany(m => m.AggregateTypes.Select(a => new AggregateTypeInfo(a, m.Domain, m.EventTypes.Count, m.CommandTypes.Count, m.ProjectionNames.Count > 0)))
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

        var queryTypesByDomain = metadata.ToDictionary(
            m => m.Domain,
            m => (IReadOnlyList<string>)[.. (m.QueryTypes ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            StringComparer.OrdinalIgnoreCase);

        return new AdminOperationalIndexSnapshot(all, normalizedByTenant, events, commands, aggregates, queryTypesByDomain);
    }

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
            [.. items.SelectMany(i => i.QueryTypes ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]);

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
    }
}

internal sealed record AdminOperationalIndexMetadataLoadResult(
    IReadOnlyList<AdminOperationalIndexDomainMetadata> Metadata,
    bool HasFailures);

/// <summary>Metadata request sent from EventStore to domain services for admin index population.</summary>
/// <param name="Domains">Configured domains for the target domain service.</param>
public sealed record AdminOperationalIndexMetadataRequest(IReadOnlyList<string> Domains);

/// <summary>Domain-service metadata response used to populate derived admin indexes.</summary>
/// <param name="Domains">Domain metadata entries.</param>
public sealed record AdminOperationalIndexMetadataResponse(IReadOnlyList<AdminOperationalIndexDomainMetadata> Domains);

/// <summary>Metadata for one EventStore domain.</summary>
public sealed record AdminOperationalIndexDomainMetadata(
    string Domain,
    IReadOnlyList<string> EventTypes,
    IReadOnlyList<string> RejectionEventTypes,
    IReadOnlyList<string> CommandTypes,
    IReadOnlyList<string> AggregateTypes,
    IReadOnlyList<string> ProjectionNames,
    IReadOnlyList<string>? QueryTypes = null);

/// <summary>Materialized admin operational index payloads ready for state-store writes.</summary>
public sealed record AdminOperationalIndexSnapshot(
    IReadOnlyList<ProjectionStatus> Projections,
    IReadOnlyDictionary<string, List<ProjectionStatus>> TenantProjections,
    IReadOnlyList<EventTypeInfo> EventTypes,
    IReadOnlyList<CommandTypeInfo> CommandTypes,
    IReadOnlyList<AggregateTypeInfo> AggregateTypes,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? QueryTypesByDomain = null);
