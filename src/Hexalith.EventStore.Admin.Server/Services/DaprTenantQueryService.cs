using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Contracts.Queries;

using Hexalith.Tenants.Contracts.Enums;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using ContractsTenantDetail = Hexalith.Tenants.Contracts.Queries.TenantDetail;
using ContractsTenantMember = Hexalith.Tenants.Contracts.Queries.TenantMember;
using ContractsTenantSummary = Hexalith.Tenants.Contracts.Queries.TenantSummary;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// Queries tenant data via the EventStore query pipeline using DAPR service invocation.
/// </summary>
public sealed class DaprTenantQueryService : ITenantQueryService
{
    private const int DefaultPageSize = 100;
    private const string QueryEndpoint = "api/v1/queries";

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly DaprClient _daprClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DaprTenantQueryService> _logger;
    private readonly AdminServerOptions _serverOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprTenantQueryService"/> class.
    /// </summary>
    public DaprTenantQueryService(
        DaprClient daprClient,
        IHttpClientFactory httpClientFactory,
        IOptions<AdminServerOptions> options,
        IAdminAuthContext authContext,
        ILogger<DaprTenantQueryService> logger)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _daprClient = daprClient;
        _httpClientFactory = httpClientFactory;
        _serverOptions = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TenantDetail?> GetTenantDetailAsync(string tenantId, CancellationToken ct = default)
    {
        try
        {
            SubmitQueryResponse response = await SendQueryAsync(
                "Tenants", tenantId, "GetTenantDetail",
                new { tenantId },
                ct).ConfigureAwait(false);

            ContractsTenantDetail? detail = response.Payload.Deserialize<ContractsTenantDetail>(_options);
            if (detail is null)
            {
                return null;
            }

            return new TenantDetail(
                detail.TenantId,
                detail.Name,
                detail.Description,
                MapStatus(detail.Status),
                detail.CreatedAt);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TenantUser>> GetTenantUsersAsync(string tenantId, CancellationToken ct = default)
    {
        List<TenantUser> allUsers = [];
        string? cursor = null;

        do
        {
            SubmitQueryResponse response = await SendQueryAsync(
                "Tenants", tenantId, "GetTenantMembers",
                new { tenantId, cursor, pageSize = DefaultPageSize },
                ct).ConfigureAwait(false);

            Hexalith.Tenants.Contracts.Queries.PaginatedResult<ContractsTenantMember>? page =
                response.Payload.Deserialize<Hexalith.Tenants.Contracts.Queries.PaginatedResult<ContractsTenantMember>>(_options);

            if (page?.Items is not null)
            {
                foreach (ContractsTenantMember m in page.Items)
                {
                    allUsers.Add(new TenantUser(m.UserId, m.Role.ToString()));
                }
            }

            cursor = page?.HasMore == true ? page.Cursor : null;
        }
        while (cursor is not null);

        return allUsers;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TenantSummary>> ListTenantsAsync(CancellationToken ct = default)
    {
        List<TenantSummary> allTenants = [];
        string? cursor = null;

        do
        {
            SubmitQueryResponse response = await SendQueryAsync(
                "Tenants", "tenant-index", "ListTenants",
                new { cursor, pageSize = DefaultPageSize },
                ct).ConfigureAwait(false);

            Hexalith.Tenants.Contracts.Queries.PaginatedResult<ContractsTenantSummary>? page =
                response.Payload.Deserialize<Hexalith.Tenants.Contracts.Queries.PaginatedResult<ContractsTenantSummary>>(_options);

            if (page?.Items is not null)
            {
                foreach (ContractsTenantSummary t in page.Items)
                {
                    allTenants.Add(new TenantSummary(t.TenantId, t.Name, MapStatus(t.Status)));
                }
            }

            cursor = page?.HasMore == true ? page.Cursor : null;
        }
        while (cursor is not null);

        return allTenants;
    }

    private static TenantStatusType MapStatus(TenantStatus status) => status switch
    {
        TenantStatus.Disabled => TenantStatusType.Disabled,
        _ => TenantStatusType.Active,
    };

    private async Task<SubmitQueryResponse> SendQueryAsync(
        string domain,
        string aggregateId,
        string queryType,
        object? payload,
        CancellationToken ct)
    {
        try
        {
            HttpRequestMessage request = _daprClient.CreateInvokeMethodRequest(
                HttpMethod.Post, _serverOptions.EventStoreAppId, QueryEndpoint);

            request.Content = JsonContent.Create(new SubmitQueryRequest(
                Tenant: string.Empty,
                Domain: domain,
                AggregateId: aggregateId,
                QueryType: queryType,
                Payload: payload is not null ? JsonSerializer.SerializeToElement(payload) : null));

            HttpClient httpClient = _httpClientFactory.CreateClient(_serverOptions.EventStoreAppId);
            HttpResponseMessage httpResponse = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
            httpResponse.EnsureSuccessStatusCode();

            SubmitQueryResponse? queryResponse = await httpResponse.Content
                .ReadFromJsonAsync<SubmitQueryResponse>(_options, ct).ConfigureAwait(false);

            return queryResponse ?? throw new InvalidOperationException(
                $"Query response body was null for {domain}/{queryType}.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Query to {_serverOptions.EventStoreAppId} timed out after {_serverOptions.ServiceInvocationTimeoutSeconds}s.");
        }
    }
}
