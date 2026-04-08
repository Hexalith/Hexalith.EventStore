using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Identity;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using ContractsMemberPage = Hexalith.Tenants.Contracts.Queries.PaginatedResult<Hexalith.Tenants.Contracts.Queries.TenantMember>;
using ContractsSummaryPage = Hexalith.Tenants.Contracts.Queries.PaginatedResult<Hexalith.Tenants.Contracts.Queries.TenantSummary>;
using ContractsTenantDetail = Hexalith.Tenants.Contracts.Queries.TenantDetail;
using ContractsTenantMember = Hexalith.Tenants.Contracts.Queries.TenantMember;
using ContractsTenantSummary = Hexalith.Tenants.Contracts.Queries.TenantSummary;
using GetTenantQuery = Hexalith.Tenants.Contracts.Queries.GetTenantQuery;
using GetTenantUsersQuery = Hexalith.Tenants.Contracts.Queries.GetTenantUsersQuery;
using ListTenantsQuery = Hexalith.Tenants.Contracts.Queries.ListTenantsQuery;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// Routes tenant queries through EventStore's query pipeline (POST /api/v1/queries).
/// </summary>
public sealed class DaprTenantQueryService : ITenantQueryService {
    private static readonly JsonSerializerOptions _deserializerOptions = new() {
        PropertyNameCaseInsensitive = true,
    };

    private const string QueryEndpoint = "api/v1/queries";
    private const int QueryPageSize = 100;

    private readonly IAdminAuthContext _authContext;
    private readonly DaprClient _daprClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DaprTenantQueryService> _logger;
    private readonly AdminServerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprTenantQueryService"/> class.
    /// </summary>
    /// <param name="daprClient">The DAPR client.</param>
    /// <param name="httpClientFactory">The HTTP client factory for DAPR service invocation.</param>
    /// <param name="options">The admin server options.</param>
    /// <param name="authContext">The admin auth context for JWT forwarding.</param>
    /// <param name="logger">The logger.</param>
    public DaprTenantQueryService(
        DaprClient daprClient,
        IHttpClientFactory httpClientFactory,
        IOptions<AdminServerOptions> options,
        IAdminAuthContext authContext,
        ILogger<DaprTenantQueryService> logger) {
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
    public async Task<TenantDetail?> GetTenantDetailAsync(string tenantId, CancellationToken ct = default) {
        try {
            SubmitQueryResponse response = await SubmitQueryAsync(
                new SubmitQueryRequest(
                    TenantIdentity.DefaultTenantId,
                    GetTenantQuery.Domain,
                    tenantId,
                    GetTenantQuery.QueryType,
                    GetTenantQuery.ProjectionType,
                    EntityId: tenantId),
                ct).ConfigureAwait(false);

            ContractsTenantDetail? detail = response.Payload.Deserialize<ContractsTenantDetail>(_deserializerOptions);
            return detail is null ? null : MapTenantDetail(detail);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TenantUser>> GetTenantUsersAsync(string tenantId, CancellationToken ct = default) {
        List<TenantUser> users = [];
        string? cursor = null;

        do {
            SubmitQueryResponse response = await SubmitQueryAsync(
                new SubmitQueryRequest(
                    TenantIdentity.DefaultTenantId,
                    GetTenantUsersQuery.Domain,
                    tenantId,
                    GetTenantUsersQuery.QueryType,
                    GetTenantUsersQuery.ProjectionType,
                    BuildPaginationPayload(cursor),
                    tenantId),
                ct).ConfigureAwait(false);

            ContractsMemberPage paginated = DeserializePayload<ContractsMemberPage>(response.Payload, GetTenantUsersQuery.QueryType);
            users.AddRange(RequireItems(paginated, GetTenantUsersQuery.QueryType).Select(MapTenantUser));
            cursor = GetNextCursor(paginated, GetTenantUsersQuery.QueryType);
        }
        while (cursor is not null);

        return users;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TenantSummary>> ListTenantsAsync(CancellationToken ct = default) {
        string? userId = _authContext.GetUserId();
        List<TenantSummary> tenants = [];
        string? cursor = null;

        try {
            do {
                SubmitQueryResponse response = await SubmitQueryAsync(
                    new SubmitQueryRequest(
                        TenantIdentity.DefaultTenantId,
                        ListTenantsQuery.Domain,
                        "index",
                        ListTenantsQuery.QueryType,
                        ListTenantsQuery.ProjectionType,
                        BuildPaginationPayload(cursor),
                        userId),
                    ct).ConfigureAwait(false);

                ContractsSummaryPage paginated = DeserializePayload<ContractsSummaryPage>(response.Payload, ListTenantsQuery.QueryType);
                tenants.AddRange(RequireItems(paginated, ListTenantsQuery.QueryType).Select(MapTenantSummary));
                cursor = GetNextCursor(paginated, ListTenantsQuery.QueryType);
            }
            while (cursor is not null);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) {
            // No tenant projection data yet — return empty list
            return tenants;
        }

        return tenants;
    }

    private static JsonElement BuildPaginationPayload(string? cursor) {
        Dictionary<string, object?> payload = new() {
            ["pageSize"] = QueryPageSize,
        };

        if (!string.IsNullOrWhiteSpace(cursor)) {
            payload["cursor"] = cursor;
        }

        return JsonSerializer.SerializeToElement(payload);
    }

    private static TPayload DeserializePayload<TPayload>(JsonElement payload, string queryType)
        => payload.Deserialize<TPayload>(_deserializerOptions)
            ?? throw new InvalidOperationException($"Query '{queryType}' returned an empty payload.");

    private static string? GetNextCursor<TPayload>(Hexalith.Tenants.Contracts.Queries.PaginatedResult<TPayload> paginated, string queryType) {
        if (!paginated.HasMore) {
            return null;
        }

        return !string.IsNullOrWhiteSpace(paginated.Cursor)
            ? paginated.Cursor
            : throw new InvalidOperationException($"Query '{queryType}' indicated more results but did not return a cursor.");
    }

    private static TenantStatusType MapStatus(TenantStatus status)
        => status switch {
            TenantStatus.Disabled => TenantStatusType.Disabled,
            _ => TenantStatusType.Active,
        };

    private static TenantDetail MapTenantDetail(ContractsTenantDetail detail)
        => new(
            detail.TenantId,
            detail.Name,
            detail.Description,
            MapStatus(detail.Status),
            detail.CreatedAt);

    private static TenantSummary MapTenantSummary(ContractsTenantSummary summary)
        => new(summary.TenantId, summary.Name, MapStatus(summary.Status));

    private static TenantUser MapTenantUser(ContractsTenantMember member)
        => new(member.UserId, member.Role.ToString());

    private static IReadOnlyList<TPayload> RequireItems<TPayload>(Hexalith.Tenants.Contracts.Queries.PaginatedResult<TPayload> paginated, string queryType)
        => paginated.Items ?? throw new InvalidOperationException($"Query '{queryType}' returned null items.");

    private async Task<SubmitQueryResponse> SubmitQueryAsync(
        SubmitQueryRequest queryRequest,
        CancellationToken ct) {
        try {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.ServiceInvocationTimeoutSeconds));

            using HttpRequestMessage httpRequest = _daprClient.CreateInvokeMethodRequest(
                HttpMethod.Post,
                _options.EventStoreAppId,
                QueryEndpoint);
            httpRequest.Content = JsonContent.Create(queryRequest);

            string? token = _authContext.GetToken();
            if (token is not null) {
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            HttpClient httpClient = _httpClientFactory.CreateClient();
            using HttpResponseMessage httpResponse = await httpClient.SendAsync(httpRequest, cts.Token).ConfigureAwait(false);
            httpResponse.EnsureSuccessStatusCode();
            return await httpResponse.Content.ReadFromJsonAsync<SubmitQueryResponse>(cts.Token).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Query '{queryRequest.QueryType}' returned an empty response body.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            _logger.LogWarning("Query {QueryType} timed out.", queryRequest.QueryType);
            throw new TimeoutException($"Query '{queryRequest.QueryType}' timed out.");
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to submit query {QueryType}.", queryRequest.QueryType);
            throw;
        }
    }
}
