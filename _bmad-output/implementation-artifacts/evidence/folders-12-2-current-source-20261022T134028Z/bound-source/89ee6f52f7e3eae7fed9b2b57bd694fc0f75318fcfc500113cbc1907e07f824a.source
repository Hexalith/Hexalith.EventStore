using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Projections;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// DAPR-backed implementation of <see cref="IProjectionQueryService"/>.
/// Projection registry reads use the state store; detail reads delegate to EventStore and
/// fall back to the Admin read-model index when EventStore does not support generic
/// projection detail (404 / 405 / 501).
/// </summary>
public sealed class DaprProjectionQueryService : IProjectionQueryService {
    /// <summary>Empty JSON object used for fallback projection configuration (AC8).</summary>
    internal const string FallbackEmptyConfiguration = "{}";

    private const string TenantAllSentinel = "all";

    private readonly IAdminAuthContext _authContext;
    private readonly DaprClient _daprClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DaprProjectionQueryService> _logger;
    private readonly AdminServerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprProjectionQueryService"/> class.
    /// </summary>
    /// <param name="daprClient">The DAPR client.</param>
    /// <param name="httpClientFactory">The HTTP client factory for DAPR service invocation.</param>
    /// <param name="options">The admin server options.</param>
    /// <param name="authContext">The admin auth context for JWT forwarding.</param>
    /// <param name="logger">The logger.</param>
    public DaprProjectionQueryService(
        DaprClient daprClient,
        IHttpClientFactory httpClientFactory,
        IOptions<AdminServerOptions> options,
        IAdminAuthContext authContext,
        ILogger<DaprProjectionQueryService> logger) {
        ArgumentNullException.ThrowIfNull(daprClient);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(authContext);
        ArgumentNullException.ThrowIfNull(logger);
        _daprClient = daprClient;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _authContext = authContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProjectionStatus>> ListProjectionsAsync(
        string? tenantId,
        CancellationToken ct = default) {
        string indexKey = $"admin:projections:{tenantId ?? TenantAllSentinel}";
        List<ProjectionStatus>? result = await _daprClient
            .GetStateAsync<List<ProjectionStatus>>(_options.StateStoreName, indexKey, cancellationToken: ct)
            .ConfigureAwait(false);

        if (result is null && tenantId is not null) {
            _logger.LogWarning(
                "Admin projection index '{IndexKey}' not found. Falling back to wildcard projection index.",
                indexKey);
            result = await _daprClient
                .GetStateAsync<List<ProjectionStatus>>(_options.StateStoreName, $"admin:projections:{TenantAllSentinel}", cancellationToken: ct)
                .ConfigureAwait(false);
            result = result?
                .Where(p => p.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase)
                    || p.TenantId.Equals(TenantAllSentinel, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.TenantId.Equals(TenantAllSentinel, StringComparison.OrdinalIgnoreCase)
                    ? CopyProjectionForTenant(p, tenantId)
                    : p)
                .ToList();
        }

        if (result is null) {
            _logger.LogWarning("Admin index '{IndexKey}' not found. Index population requires admin projection setup.", indexKey);
            return [];
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<ProjectionDetail?> GetProjectionDetailAsync(
        string tenantId,
        string projectionName,
        CancellationToken ct = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionName);

        string endpoint = $"api/v1/admin/projections/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(projectionName)}";
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.ServiceInvocationTimeoutSeconds));

        using HttpRequestMessage request = _daprClient.CreateInvokeMethodRequest(
            HttpMethod.Get, _options.EventStoreAppId, endpoint)
            ?? new HttpRequestMessage(HttpMethod.Get, endpoint);

        string? token = _authContext.GetToken();
        if (token is not null) {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        HttpClient httpClient = _httpClientFactory.CreateClient();
        using HttpResponseMessage httpResponse = await httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);

        if (IsDetailUnsupportedStatus(httpResponse.StatusCode)) {
            return await BuildFallbackProjectionDetailAsync(tenantId, projectionName, httpResponse.StatusCode, cts.Token)
                .ConfigureAwait(false);
        }

        _ = httpResponse.EnsureSuccessStatusCode();
        return await httpResponse.Content.ReadFromJsonAsync<ProjectionDetail>(cts.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the EventStore response indicates the generic
    /// projection-detail endpoint is missing or unsupported, and Admin read-model fallback is
    /// allowed (AC6).
    /// </summary>
    private static bool IsDetailUnsupportedStatus(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.NotFound
            or HttpStatusCode.MethodNotAllowed
            or HttpStatusCode.NotImplemented;

    private async Task<ProjectionDetail?> BuildFallbackProjectionDetailAsync(
        string tenantId,
        string projectionName,
        HttpStatusCode upstreamStatus,
        CancellationToken ct) {
        ProjectionIndexHit? indexed = await FindProjectionInAdminIndexAsync(tenantId, projectionName, ct)
            .ConfigureAwait(false);

        if (indexed is null) {
            _logger.LogInformation(
                "Projection detail fallback miss for tenant '{TenantId}' projection '{ProjectionName}' (upstream status {UpstreamStatus}); projection absent from admin indexes.",
                tenantId,
                projectionName,
                (int)upstreamStatus);
            return null;
        }

        _logger.LogInformation(
            "Projection detail fallback used for tenant '{TenantId}' projection '{ProjectionName}' (upstream status {UpstreamStatus}, source '{FallbackSourceKey}').",
            tenantId,
            projectionName,
            (int)upstreamStatus,
            indexed.SourceKey);

        ProjectionStatus status = indexed.Status;
        return new ProjectionDetail(
            status.Name,
            tenantId,
            status.Status,
            status.Lag,
            status.Throughput,
            status.ErrorCount,
            status.LastProcessedPosition,
            status.LastProcessedUtc,
            [],
            FallbackEmptyConfiguration,
            []);
    }

    private async Task<ProjectionIndexHit?> FindProjectionInAdminIndexAsync(
        string tenantId,
        string projectionName,
        CancellationToken ct) {
        // Tenant-scoped index takes precedence (AC7).
        string tenantIndexKey = $"admin:projections:{tenantId}";
        List<ProjectionStatus>? tenantIndex = await _daprClient
            .GetStateAsync<List<ProjectionStatus>>(_options.StateStoreName, tenantIndexKey, cancellationToken: ct)
            .ConfigureAwait(false);

        ProjectionStatus? hit = tenantIndex?
            .FirstOrDefault(p => IsProjectionMatch(p, tenantId, projectionName));
        if (hit is not null) {
            return new ProjectionIndexHit(hit, tenantIndexKey);
        }

        // Fallback to the wildcard index, but only honour tenant-neutral or tenant-matching rows
        // (AC7 — never return a detail for a different tenant).
        string allIndexKey = $"admin:projections:{TenantAllSentinel}";
        List<ProjectionStatus>? allIndex = await _daprClient
            .GetStateAsync<List<ProjectionStatus>>(_options.StateStoreName, allIndexKey, cancellationToken: ct)
            .ConfigureAwait(false);

        hit = allIndex?
            .FirstOrDefault(p => IsProjectionMatch(p, tenantId, projectionName));
        return hit is null ? null : new ProjectionIndexHit(hit, allIndexKey);
    }

    private static bool IsProjectionMatch(ProjectionStatus projection, string tenantId, string projectionName)
        => string.Equals(projection.Name, projectionName, StringComparison.Ordinal)
            && (projection.TenantId.Equals(TenantAllSentinel, StringComparison.OrdinalIgnoreCase)
                || projection.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase));

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

    private sealed record ProjectionIndexHit(ProjectionStatus Status, string SourceKey);
}
